using System.Security.Claims;
using ModelContextProtocol;

namespace TodoAPI.Auth;

/// <summary>
/// Decides which tenant's data a tool call touches. Tenants map to Entra groups in appsettings.json ("Tenants").
/// In production this is usually the token's tid claim; our demo users share one Entra tenant, so a group stands in for it.
/// </summary>
public sealed class TenantResolver(IConfiguration config)
{
    public string Resolve(ClaimsPrincipal? user, string requestedTenant)
    {
        var tenant = requestedTenant;

        // DEMO 3: Tenant comes from the token. See DEMO.md.
        // Before: the model picks the tenant, so Bob (Fabrikam) can read Contoso. After: Bob's Entra group decides; asking for another tenant is denied.
        //tenant = FromToken(user, requestedTenant);

        return tenant;
    }

    private string FromToken(ClaimsPrincipal? user, string requestedTenant)
    {
        if (user is null) throw new McpException("Sign in first: the tenant comes from your token.");
        var groups = user.FindAll("groups").Select(c => c.Value).ToHashSet();
        var tenant = config.GetSection("Tenants").GetChildren().FirstOrDefault(t => groups.Contains(t.Value ?? ""))?.Key
            ?? throw new McpException("You aren't a member of any tenant.");

        // Refuse rather than silently swap, so the attempt shows up as DENIED in the audit log.
        return string.Equals(tenant, requestedTenant, StringComparison.OrdinalIgnoreCase)
            ? tenant
            : throw new McpException($"{Name(user)} is in {tenant}, not {requestedTenant}.");
    }

    private static string Name(ClaimsPrincipal user) =>
        user.FindFirst("name")?.Value ?? user.FindFirst("preferred_username")?.Value ?? "You";
}
