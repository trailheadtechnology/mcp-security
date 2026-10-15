using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TodoAPI.Auth;
using TodoMcp.Shared;

// Every tool takes a tenantId from the model, then asks TenantResolver which tenant it really gets (demo 3).
// ClaimsPrincipal and TenantResolver are injected by the SDK and never appear in the tool schema.
[McpServerToolType]
public static class TodoMcpTools
{
    private const string TenantDescription = "The tenant (customer) whose todos these are, e.g. contoso or fabrikam";

    [McpServerTool]
    [Description("Show who the server thinks is calling: name, roles, groups, and scopes from the access token.")]
    public static string WhoAmI(ClaimsPrincipal user) => JsonSerializer.Serialize(new
    {
        name = user.FindFirst("name")?.Value ?? user.FindFirst("preferred_username")?.Value,
        isAdmin = user.IsInRole("Todos.Admin"),
        roles = user.FindAll("roles").Select(c => c.Value),
        groups = user.FindAll("groups").Select(c => c.Value),
        scopes = user.FindFirst("scp")?.Value,
    });

    [McpServerTool]
    [Description("Get a tenant's todos. Can optionally filter by completion status.")]
    public static string GetTodos(
        TenantResolver tenants,
        ClaimsPrincipal user,
        [Description(TenantDescription)] string tenantId,
        [Description("Filter by completion status: true for completed items, false for incomplete items, or omit to get all items")]
        bool? isComplete = null)
    {
        var tenant = tenants.Resolve(user, tenantId);
        return JsonSerializer.Serialize(new { tenant, todos = TodoStore.GetAll(tenant, isComplete) });
    }

    [RequiresScope("Todos.Write")]
    [McpServerTool]
    [Description("Add a single todo to a tenant's list.")]
    public static string AddTodo(
        TenantResolver tenants,
        ClaimsPrincipal user,
        [Description(TenantDescription)] string tenantId,
        [Description("The title of the todo to add")] string title)
    {
        var added = TodoStore.Add(tenants.Resolve(user, tenantId), title);
        return JsonSerializer.Serialize(added);
    }

    [RequiresScope("Todos.Write")]
    [McpServerTool]
    [Description("Add multiple todos to a tenant's list at once. Use this when the user wants to add several items. Provide a JSON array of title strings.")]
    public static string AddMultipleTodos(
        TenantResolver tenants,
        ClaimsPrincipal user,
        [Description(TenantDescription)] string tenantId,
        [Description("JSON array of todo titles, e.g. [\"Buy milk\", \"Walk dog\", \"Send email\"]")] string titlesJson)
    {
        var tenant = tenants.Resolve(user, tenantId);
        var titles = JsonSerializer.Deserialize<string[]>(titlesJson) ?? Array.Empty<string>();
        var addedTodos = titles.Select(title => TodoStore.Add(tenant, title)).ToList();
        return JsonSerializer.Serialize(addedTodos);
    }

    [RequiresScope("Todos.Write")]
    [McpServerTool, Description("Mark a todo item as complete.")]
    public static string MarkTodoComplete(
        TenantResolver tenants,
        ClaimsPrincipal user,
        [Description(TenantDescription)] string tenantId,
        [Description("The ID of the todo to mark as complete")] int id)
    {
        var tenant = tenants.Resolve(user, tenantId);
        var updated = TodoStore.MarkComplete(tenant, id) ?? throw new McpException($"Todo {id} not found in tenant {tenant}");
        return JsonSerializer.Serialize(updated);
    }

    [RequiresScope("Todos.Write")]
    [McpServerTool]
    [Description("Mark a todo item as incomplete (not done).")]
    public static string MarkTodoIncomplete(
        TenantResolver tenants,
        ClaimsPrincipal user,
        [Description(TenantDescription)] string tenantId,
        [Description("The ID of the todo to mark as incomplete")] int id)
    {
        var tenant = tenants.Resolve(user, tenantId);
        var updated = TodoStore.MarkIncomplete(tenant, id) ?? throw new McpException($"Todo {id} not found in tenant {tenant}");
        return JsonSerializer.Serialize(updated);
    }

    // DEMO 2: Two users, two tool lists. See DEMO.md.
    // Before: Bob (no role) sees and can call this. 
    // After: only Todos.Admin users (Alice) get it in tools/list.
    //[Authorize(Roles = "Todos.Admin")]
    [RequiresScope("Todos.Write")]
    [McpServerTool]
    [Description("Delete every todo on a tenant's list.")]
    public static string ClearAllTodos(
        TenantResolver tenants,
        ClaimsPrincipal user,
        [Description(TenantDescription)] string tenantId)
    {
        var tenant = tenants.Resolve(user, tenantId);
        return JsonSerializer.Serialize(new { tenant, removed = TodoStore.Clear(tenant) });
    }
}
