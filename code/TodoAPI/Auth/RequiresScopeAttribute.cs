namespace TodoAPI.Auth;

/// <summary>
/// Marks a tool as needing an OAuth scope the user may not have granted yet.
/// Unlike [Authorize], the tool stays visible in tools/list; calling it without the scope
/// returns HTTP 403 insufficient_scope so the client can ask the user for more consent (step-up).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresScopeAttribute(string scopeName) : Attribute
{
    public string ScopeName { get; } = scopeName;
}
