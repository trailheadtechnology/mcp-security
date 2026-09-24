using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;

namespace TodoAPI.Auth;

/// <summary>
/// Protects the MCP endpoint with OAuth, using Entra ID as the identity provider.
/// Wire-up is two calls from Program.cs: builder.AddEntraOAuth() and app.UseEntraOAuth().
///
/// Entra app registration prerequisites (single-tenant, public client, no secret):
///   - Authentication: "Mobile and desktop applications" platform with redirect URIs
///       http://localhost/callback            (Claude Code)
///       http://localhost/oauth/callback      (mcp-remote)
///       http://127.0.0.1/oauth/callback      (mcp-remote, alternate)
///     and "Allow public client flows" = Yes
///   - Expose an API: App ID URI api://{client-id}, scope "Todos.Access" (admins and users),
///     and the app's own client id added under "Authorized client applications"
///   - Manifest: requestedAccessTokenVersion = 2
/// </summary>
public static class EntraAuthExtensions
{
    public static WebApplicationBuilder AddEntraOAuth(this WebApplicationBuilder builder, string serverUrl)
    {
        var entra = builder.Configuration.GetSection(EntraOptions.SectionName).Get<EntraOptions>() ?? new();
        entra.Validate();
        builder.Services.Configure<EntraOptions>(builder.Configuration.GetSection(EntraOptions.SectionName));
        builder.Services.AddHttpClient();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            // Validates the bearer tokens Entra issues for our API scope.
            .AddJwtBearer(options =>
            {
                options.Authority = entra.Issuer;
                options.MapInboundClaims = false; // keep Entra's claim names (scp, oid, roles) as-is
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Entra issues v1 or v2 tokens depending on the app manifest's requestedAccessTokenVersion.
                    // Accept both so the demo doesn't hinge on that setting.
                    ValidIssuers = [entra.Issuer, $"https://sts.windows.net/{entra.TenantId}/"],
                    ValidAudiences = [entra.ClientId, $"api://{entra.ClientId}"],
                };
            })
            // On 401, tells MCP clients where to find the protected resource metadata (RFC 9728),
            // and serves that metadata at /.well-known/oauth-protected-resource.
            // Same as .AddMcp(...), but with a challenge that starts the user off with read-only consent.
            .AddScheme<McpAuthenticationOptions, ProgressiveMcpAuthenticationHandler>(
                McpAuthenticationDefaults.AuthenticationScheme, McpAuthenticationDefaults.DisplayName, options =>
            {
                options.ResourceMetadata = new()
                {
                    Resource = $"{serverUrl}/mcp",
                    AuthorizationServers = { serverUrl }, // our shim, which fronts Entra
                    ScopesSupported = [entra.ReadScope, entra.WriteScope],
                };
            });

        builder.Services.AddAuthorization();
        return builder;
    }

    public static WebApplication UseEntraOAuth(this WebApplication app, string serverUrl)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEntraAuthShim(serverUrl);
        return app;
    }
}
