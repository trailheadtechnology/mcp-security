using System.Threading;

namespace TodoMcp.Shared;

/// <summary>
/// In-memory todos, partitioned by tenant. Every method takes the tenant, so the store never
/// decides on its own whose data it's touching. Who gets to pick the tenant is the caller's problem (demo 3).
/// </summary>
public static class TodoStore
{
    private static readonly List<Todo> _todos = new();
    private static int _nextId = 0;

    static TodoStore()
    {
        Add("contoso", "Renew the Contoso support contract");
        Add("contoso", "Contoso Q4 budget review");
        Add("fabrikam", "Fabrikam launch checklist");
        Add("fabrikam", "Fabrikam payroll export");
    }

    public static IEnumerable<Todo> GetAll(string tenantId, bool? isComplete = null) =>
        _todos.Where(t => t.TenantId == tenantId && (isComplete == null || t.IsComplete == isComplete.Value));

    public static Todo Add(string tenantId, string title)
    {
        var todo = new Todo(Interlocked.Increment(ref _nextId), title, false, tenantId);
        _todos.Add(todo);
        return todo;
    }

    public static int Clear(string tenantId) => _todos.RemoveAll(t => t.TenantId == tenantId);

    public static Todo? MarkComplete(string tenantId, int id) => SetComplete(tenantId, id, true);

    public static Todo? MarkIncomplete(string tenantId, int id) => SetComplete(tenantId, id, false);

    // Looks up by id *within the tenant*: another tenant's todo is "not found", never "forbidden",
    // so callers can't probe which ids exist elsewhere.
    private static Todo? SetComplete(string tenantId, int id, bool isComplete)
    {
        var index = _todos.FindIndex(t => t.Id == id && t.TenantId == tenantId);
        if (index < 0) return null;

        var updated = _todos[index] with { IsComplete = isComplete };
        _todos[index] = updated;
        return updated;
    }
}
