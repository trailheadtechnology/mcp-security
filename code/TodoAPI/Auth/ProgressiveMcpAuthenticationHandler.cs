using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;

namespace TodoAPI.Auth;

/// <summary>
/// The SDK's MCP challenge, plus a scope hint: the first 401 asks only for read access.
/// Without it, clients fall back to requesting everything in scopes_supported, and there's nothing left to step up to.
/// </summary>
public sealed class ProgressiveMcpAuthenticationHandler(
    IOptionsMonitor<McpAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<EntraOptions> entra)
    : McpAuthenticationHandler(options, logger, encoder)
{
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await base.HandleChallengeAsync(properties);
        Response.Headers.WWWAuthenticate = $"{Response.Headers.WWWAuthenticate}, scope=\"{entra.Value.ReadScope}\"";
    }
}
