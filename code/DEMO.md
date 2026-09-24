# Demo toggles

`main` starts wide open. Each feature is a commented-out line marked `// DEMO:`.
Uncomment them in this order during the talk (`grep -rn "// DEMO:" code/TodoAPI` finds them all).

| # | Feature | Where | What to show |
|---|---|---|---|
| 1 | Lock the door | `Program.cs`: `//mcp.RequireAuthorization();` | 401 → protected resource metadata → Entra sign-in → token |
| 2 | Step-up auth | `Program.cs`: `//app.UseStepUpAuthorization(ServerUrl);` | Before: a read-only token can still add todos. After: 403 `insufficient_scope` → Todos.Write consent → retry |

Before step 2 is on, the first sign-in only asks for `Todos.Read`, yet writes still succeed. The token says read-only; nothing checks it.

## Rehearsal check

`TodoAPI.Tests` describes the finished state. Uncomment every `// DEMO:` line, then run `dotnet test`; all tests should pass. With the toggles commented out, the step-up tests fail by design.
