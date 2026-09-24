namespace TodoAPI.Auth;

/// <summary>Bound from the "Entra" section of appsettings.json.</summary>
public sealed class EntraOptions
{
    public const string SectionName = "Entra";

    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";

    /// <summary>Scopes exposed on the app registration ("Expose an API" in the portal).</summary>
    public string ReadScopeName { get; set; } = "Todos.Read";
    public string WriteScopeName { get; set; } = "Todos.Write";

    public string Issuer => $"https://login.microsoftonline.com/{TenantId}/v2.0";
    public string AuthorizeEndpoint => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize";
    public string TokenEndpoint => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";

    /// <summary>Requests use fully-qualified scopes (api://{client-id}/Todos.Read); the token's scp claim holds the short names.</summary>
    public string ScopeUri(string scopeName) => $"api://{ClientId}/{scopeName}";
    public string ReadScope => ScopeUri(ReadScopeName);
    public string WriteScope => ScopeUri(WriteScopeName);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId) || string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException(
                "OAuth demo is enabled but Entra:TenantId / Entra:ClientId are empty in appsettings.json.");
        }
    }
}
