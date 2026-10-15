using System.Security.Claims;
using ModelContextProtocol;

namespace TodoAPI.Auth;

/// <summary>
/// Decides which tenant's data a tool call touches. Tenants map to Entra groups in appsettings.json ("Tenants").
/// In production this is usually the token's tid claim; our demo users share one Entra tenant, so a group stands in for it.
/// </summary>
public sealed class TenantResolver(IConfiguration config)
{
    public string Resolve(ClaimsPrincipal user, string requestedTenant)
    {
        var tenant = requestedTenant;

        // DEMO 3: Tenant comes from the token. See DEMO.md.
        // Before: the model picks the tenant, so Bob (Fabrikam) can read Contoso. After: Bob's Entra group decides; the model's tenantId is ignored.
        //tenant = FromToken(user);

        return tenant;
    }

    private string FromToken(ClaimsPrincipal user)
    {
        var groups = user.FindAll("groups").Select(c => c.Value).ToHashSet();
        return config.GetSection("Tenants").GetChildren().FirstOrDefault(t => groups.Contains(t.Value ?? ""))?.Key
            ?? throw new McpException("You aren't a member of any tenant.");
    }
}
