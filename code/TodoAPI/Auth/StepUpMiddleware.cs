using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace TodoAPI.Auth;

/// <summary>
/// Step-up authorization (MCP spec: "Scope Challenge Handling").
/// When a tool marked [RequiresScope] is called with a token that lacks the scope, answer with
/// HTTP 403 + WWW-Authenticate: Bearer error="insufficient_scope", scope="..." so the client re-authorizes
/// with the extra scope and retries. This has to happen at the HTTP layer: [Authorize] failures inside
/// MCP come back as JSON-RPC errors, which clients don't treat as a reason to re-authorize.
/// </summary>
public sealed class StepUpMiddleware(
    RequestDelegate next,
    IOptions<McpServerOptions> mcpOptions,
    IOptions<EntraOptions> entra,
    string resourceMetadataUrl)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var toolName = await ReadCalledToolNameAsync(context.Request);
        var requiredScope = mcpOptions.Value.ToolCollection?
            .FirstOrDefault(t => t.ProtocolTool.Name == toolName)?
            .Metadata.OfType<RequiresScopeAttribute>().FirstOrDefault()?.ScopeName;

        if (requiredScope is null || HasScope(context.User, requiredScope))
        {
            await next(context);
            return;
        }

        // Ask for the new scope *plus* what they already have, so re-authorizing doesn't drop read access.
        var scopes = GrantedScopes(context.User).Append(requiredScope).Distinct().Select(entra.Value.ScopeUri);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers.WWWAuthenticate =
            $"Bearer error=\"insufficient_scope\", scope=\"{string.Join(' ', scopes)}\", " +
            $"resource_metadata=\"{resourceMetadataUrl}\", " +
            $"error_description=\"The {toolName} tool requires the {requiredScope} scope\"";
    }

    private static bool HasScope(ClaimsPrincipal user, string scope) => GrantedScopes(user).Contains(scope);

    // Entra puts delegated scopes in a single space-separated "scp" claim.
    private static IEnumerable<string> GrantedScopes(ClaimsPrincipal user) =>
        user.FindFirst("scp")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static async Task<string?> ReadCalledToolNameAsync(HttpRequest request)
    {
        // Always read the body, never the Mcp-Name header: the SDK only checks that header against the body
        // for 2026-07-28 requests, so a client claiming an older version could name one tool and call another.
        request.EnableBuffering();
        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("method", out var method) && method.GetString() == "tools/call"
                && root.TryGetProperty("params", out var p) && p.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null; // Not our problem; let the MCP handler reject it.
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}

public static class StepUpExtensions
{
    /// <summary>Runs step-up checks on /mcp. Must come after UseEntraOAuth (it needs the authenticated user).</summary>
    public static WebApplication UseStepUpAuthorization(this WebApplication app, string serverUrl)
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/mcp"),
            mcp => mcp.UseMiddleware<StepUpMiddleware>($"{serverUrl}/.well-known/oauth-protected-resource/mcp"));
        return app;
    }
}
