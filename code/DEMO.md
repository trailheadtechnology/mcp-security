# Demo toggles

`main` starts wide open. Each feature is a commented-out line under a `// DEMO` comment.
Uncomment them in this order during the talk (`grep -rn "// DEMO" code/TodoAPI` finds them all).

| # | Feature | Where | What to show |
|---|---|---|---|
| 1 | Lock the door | `Program.cs`: `//mcp.RequireAuthorization();` | 401 → protected resource metadata → Entra sign-in → token |
| 2 | Two users, two tool lists | `TodoMcpTools.cs`: `//[Authorize(Roles = "Todos.Admin")]` | Before: Bob (no role) sees `clear_all_todos`. After: Alice still sees it, Bob doesn't |
| 3a | Tenant comes from the token | `Auth/TenantResolver.cs`: `//tenant = FromToken(user);` | Before: Bob (Fabrikam) asks for Contoso's todos and gets them. After: he gets Fabrikam's, whatever he asks for |
| 3b | Audit trail | `Program.cs`: `//mcpServer.WithAuditLog();` | One `AUDIT` line per tool call in terminal 1, including a `DENIED` |
| extra | Step-up auth (no slide) | `Program.cs`: `//app.UseStepUpAuthorization(ServerUrl);` | Before: a read-only token can still add todos. After: 403 `insufficient_scope` → Todos.Write consent → retry |

Before step-up is on, the first sign-in only asks for `Todos.Read`, yet writes still succeed. The token says read-only; nothing checks it.

## Setup

### Entra (once)

In the Entra admin center (entra.microsoft.com), in the tenant from `appsettings.json`.
Signing in with the personal Microsoft account that created the tenant is enough: it's the tenant's admin.
Alice and Bob are cloud-only users inside the tenant: no mailbox, no license, no cost.

1. **Users → All users → New user → Create new user**: Alice and Bob, at the domain shown under **Overview → Primary domain** (e.g. `bob@<something>.onmicrosoft.com`). Copy each temporary password.
2. **Groups → New group** (Security): `Contoso` with Alice, `Fabrikam` with Bob. Paste each group's **Object ID** into `Tenants` in `appsettings.json`.
3. App registration → **Token configuration → Add groups claim** → Security groups, access token: Group ID.
4. App registration → **App roles** → `Todos.Admin`, allowed member type Users/Groups.
5. Enterprise applications → the app → **Users and groups** → assign Alice to `Todos.Admin`. Bob gets no role.

### Entra pages to show

`./entra-tabs.sh` opens them in a new Chrome window: App roles, the role assignment (Manage view → Edit columns → add **Role assigned**), and Contoso and Fabrikam members.
Open it before the talk and pick the admin account in each tab, so the account picker doesn't appear on stage.

### Signing in as a demo user

The sign-in page always shows Entra's account picker, so each user is one click once the browser knows them.

1. In Claude: `/mcp` → TodoAPI → **Authenticate**. The browser opens on the account picker.
2. **Use another account** → `bob@<tenant>.onmicrosoft.com` → temporary password → set a new one (and MFA, if the tenant asks).
3. Back in Claude, TodoAPI shows as connected. Claude is now Bob.

To switch: `/mcp` → TodoAPI → **Clear authentication** → **Authenticate** → pick the other account.
Do the first sign-in for both users while rehearsing, so the password change and MFA setup never happen on stage.

### Terminals

Terminal 1 (server restarts on every save, so a toggle is just uncomment + save):

```bash
cd code/TodoAPI
dotnet watch --no-hot-reload
```

Console logging is off, so it prints nothing once running (until the audit log is on). Check http://localhost:5555/swagger.

Terminal 2:

```bash
cd code/TodoAPI
claude
```

Start the server first. Approve the `TodoAPI` server from `.mcp.json` when asked, then `/mcp` should show it connected.

## Demo 2: users

Signed in as Bob (from demo 1):
1. `/mcp` → TodoAPI → view tools: `clear_all_todos` is there.
2. Uncomment `[Authorize(Roles = "Todos.Admin")]`, save. `/mcp` → **Reconnect**. As Bob, the tool is gone. Ask Claude to clear the list; it has no way to.
3. Switch to Alice. The tool is back.

## Demo 3: tenants and audit

Seed data: Contoso has todos 1–2, Fabrikam has 3–4. Each restart reseeds.
Switch back to Bob (Fabrikam) first.

1. "list Contoso's todos": Bob gets Contoso's list. The model picked the tenant.
2. Uncomment `tenant = FromToken(user, requestedTenant);` and `mcpServer.WithAuditLog();`, save, `/mcp` → **Reconnect**.
3. "list Contoso's todos" again: denied, "Bob is in fabrikam, not contoso". The model still sends `contoso`; the token wins.
4. "list my todos": Fabrikam's list (3–4).
5. Terminal 1: a `DENIED` line for the Contoso attempt, then an `ALLOWED` line, each with Bob's name, the agent, the tool, and the arguments.

`[Authorize]` rejections (demo 2) happen before the audit filter, so they don't appear in the log.

## Rehearsal check

`TodoAPI.Tests` describes the finished state. Uncomment every `// DEMO` line, then run `dotnet test`; all tests should pass. With the toggles commented out, the step-up, role, and tenant tests fail by design.

To reset after a rehearsal, comment the toggles back out and `/mcp` → TodoAPI → **Clear authentication**, so the sign-in shows again on stage.
