using TodoAPI.Auth;

const string ServerUrl = "http://localhost:5555";

var builder = WebApplication.CreateBuilder(args);

// Remove console logging to keep stdout clean for stdio transport
// (HTTP transport doesn't need stdout, so this is safe)
builder.Logging.ClearProviders();
builder.Logging.AddDebug();

builder.WebHost.UseUrls(ServerUrl);

// Add OpenAPI/Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add MCP server with HTTP transport
var mcpServer = builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .AddAuthorizationFilters(); // honors [Authorize] on tools: hides them from tools/list and blocks calls
builder.Services.AddSingleton<TenantResolver>();

// DEMO 3 (audit): Log every tool call. See DEMO.md.
// Before: server terminal is silent. After: one AUDIT line per call, with who, what, and ALLOWED or DENIED.
//mcpServer.WithAuditLog();

// OAuth: protect the MCP endpoint with Entra ID (see Auth/ and appsettings.json)
builder.AddEntraOAuth(ServerUrl);

var app = builder.Build();

// Swagger endpoints
app.UseSwagger();
app.UseSwaggerUI();

// OAuth: authentication middleware + the endpoints MCP clients use to discover and register
app.UseEntraOAuth(ServerUrl);

// DEMO (extra, no slide): Step-up auth. See DEMO.md.
// Before: a Todos.Read token can still add todos. After: write tools answer 403 insufficient_scope
// and Claude Code asks the user to consent to Todos.Write, then retries.
//app.UseStepUpAuthorization(ServerUrl);

// MCP HTTP endpoint
var mcp = app.MapMcp("/mcp");

// DEMO 1: Lock the door. See DEMO.md.
// Before: anyone can call the tools. After: 401 → Claude Code finds the metadata → Entra sign-in → token.
//mcp.RequireAuthorization();

// REST endpoints
app.MapEndpoints();

await app.RunAsync();
public partial class Program;
