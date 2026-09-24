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
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// OAuth: protect the MCP endpoint with Entra ID (see Auth/ and appsettings.json)
builder.AddEntraOAuth(ServerUrl);

var app = builder.Build();

// Swagger endpoints
app.UseSwagger();
app.UseSwaggerUI();

// OAuth: authentication middleware + the endpoints MCP clients use to discover and register
app.UseEntraOAuth(ServerUrl);

// DEMO: Step-up auth. Write tools ([RequiresScope]) answer 403 insufficient_scope until the user consents to Todos.Write.
//app.UseStepUpAuthorization(ServerUrl);

// MCP HTTP endpoint
var mcp = app.MapMcp("/mcp");

// DEMO: Lock the door. No token, no MCP.
//mcp.RequireAuthorization();

// REST endpoints
app.MapEndpoints();

await app.RunAsync();
public partial class Program;
