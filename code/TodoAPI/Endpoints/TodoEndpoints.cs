using TodoMcp.Shared;

// Unauthenticated REST API for poking at the store from Swagger. The caller names the tenant.
public class TodoEndpoints : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/{tenantId}");
        api.MapGet("/todos", GetTodosHandler);
        api.MapPost("/todos", AddTodoHandler);
        api.MapPatch("/todos/{id}/complete", MarkTodoCompleteHandler);
        api.MapPatch("/todos/{id}/incomplete", MarkTodoIncompleteHandler);
    }

    private static IResult GetTodosHandler(string tenantId, bool? isComplete)
    {
        var todos = TodoStore.GetAll(tenantId, isComplete);
        return Results.Ok(todos);
    }

    private static IResult AddTodoHandler(string tenantId, Todo todo)
    {
        var added = TodoStore.Add(tenantId, todo.Title);
        return Results.Created($"/api/{tenantId}/todos/{added.Id}", added);
    }

    private static IResult MarkTodoCompleteHandler(string tenantId, int id)
    {
        var updated = TodoStore.MarkComplete(tenantId, id);
        return updated == null ? Results.NotFound() : Results.Ok(updated);
    }

    private static IResult MarkTodoIncompleteHandler(string tenantId, int id)
    {
        var updated = TodoStore.MarkIncomplete(tenantId, id);
        return updated == null ? Results.NotFound() : Results.Ok(updated);
    }
}
