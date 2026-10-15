using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TodoMcp.Shared;

namespace TodoAPI.Tests;

/// <summary>
/// Progressive auth, role-based tool lists, tenants, and the audit log, without Entra: tokens are signed locally with a test key that replaces Entra's signing keys.
/// Values match appsettings.json so the server's issuer/audience checks still run for real.
/// These describe the finished demo: uncomment every "// DEMO" toggle before running them (see DEMO.md).
/// </summary>
public class StepUpTests(StepUpTests.Factory factory) : IClassFixture<StepUpTests.Factory>
{
    private const string TenantId = "71fba380-7938-4456-9389-e94c9aefcd1f";
    private const string ClientId = "07f59dcc-1a3f-456f-94a9-f79da75c8326";
    private const string ReadScope = $"api://{ClientId}/Todos.Read";
    private const string WriteScope = $"api://{ClientId}/Todos.Write";

    private const string ContosoGroup = "contoso-group-id";
    private const string FabrikamGroup = "fabrikam-group-id";

    private static readonly SymmetricSecurityKey TestKey = new(Encoding.UTF8.GetBytes(new string('k', 64)));

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder
            .UseSetting("Tenants:contoso", ContosoGroup)
            .UseSetting("Tenants:fabrikam", FabrikamGroup)
            .ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration { Issuer = $"https://login.microsoftonline.com/{TenantId}/v2.0" };
                    options.TokenValidationParameters.IssuerSigningKey = TestKey;
                }));
    }

    [Fact]
    public async Task No_token_gets_401_asking_for_read_scope_only()
    {
        var response = await PostMcp(token: null, ToolsList);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("resource_metadata=", challenge);
        Assert.Contains($"scope=\"{ReadScope}\"", challenge);
        Assert.DoesNotContain("Todos.Write", challenge);
    }

    [Fact]
    public async Task Metadata_advertises_both_scopes()
    {
        var json = await factory.CreateClient().GetStringAsync("/.well-known/oauth-protected-resource/mcp");

        Assert.Contains(ReadScope, json);
        Assert.Contains(WriteScope, json);
    }

    [Fact]
    public async Task Read_only_token_still_sees_write_tools()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsList);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("add_todo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Read_only_token_can_call_read_tool()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsCall("get_todos", """{"tenantId":"contoso"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Read_only_token_calling_write_tool_gets_step_up_challenge()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsCall("add_todo", """{"tenantId":"contoso","title":"Buy milk"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"insufficient_scope\"", challenge);
        Assert.Contains($"scope=\"{ReadScope} {WriteScope}\"", challenge); // keeps read, adds write
        Assert.Contains("resource_metadata=", challenge);
    }

    [Fact]
    public async Task Read_write_token_can_call_write_tool()
    {
        var response = await PostMcp(Token("Todos.Read Todos.Write"), ToolsCall("add_todo", """{"tenantId":"contoso","title":"Buy milk"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Buy milk", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lying_Mcp_Name_header_does_not_bypass_step_up()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsCall("add_todo", """{"tenantId":"contoso","title":"Sneaky"}"""),
            request =>
            {
                request.Headers.Add("MCP-Protocol-Version", "2025-11-25"); // older version: SDK won't cross-check headers
                request.Headers.Add("Mcp-Method", "tools/call");
                request.Headers.Add("Mcp-Name", "get_todos");
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_sees_admin_tool()
    {
        var response = await PostMcp(Token("Todos.Read", role: "Todos.Admin"), ToolsList);

        Assert.Contains("clear_all_todos", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Viewer_does_not_see_admin_tool()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsList);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("clear_all_todos", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Viewer_cannot_call_admin_tool_by_name()
    {
        var response = await PostMcp(Token("Todos.Read Todos.Write"), ToolsCall("clear_all_todos", """{"tenantId":"contoso"}"""));

        Assert.DoesNotContain("\"removed\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tenant_comes_from_the_token_not_the_model()
    {
        // Bob is in Fabrikam's group but asks for Contoso.
        var response = await PostMcp(Token("Todos.Read", group: FabrikamGroup), ToolsCall("get_todos", """{"tenantId":"contoso"}"""));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Fabrikam launch checklist", body);
        Assert.DoesNotContain("Contoso", body);
    }

    [Fact]
    public async Task Other_tenants_todo_is_not_found()
    {
        var contosoTodo = TodoStore.GetAll("contoso").First().Id;

        var response = await PostMcp(Token("Todos.Read Todos.Write", group: FabrikamGroup),
            ToolsCall("mark_todo_complete", $$"""{"tenantId":"contoso","id":{{contosoTodo}}}"""));

        Assert.Contains($"Todo {contosoTodo} not found in tenant fabrikam", await response.Content.ReadAsStringAsync());
        Assert.False(TodoStore.GetAll("contoso").First().IsComplete);
    }

    [Fact]
    public async Task Audit_log_records_allows_and_denies()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await PostMcp(Token("Todos.Read", group: FabrikamGroup), ToolsCall("get_todos", """{"tenantId":"fabrikam"}"""));
            await PostMcp(Token("Todos.Read Todos.Write", group: FabrikamGroup), ToolsCall("mark_todo_complete", """{"tenantId":"contoso","id":999}"""));
        }
        finally
        {
            Console.SetOut(original);
        }

        var log = output.ToString();
        Assert.Contains("ALLOWED | Alice", log);
        Assert.Contains("get_todos", log);
        Assert.Contains("DENIED  Todo 999 not found in tenant fabrikam", log);
    }

    private const string ToolsList = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    private static string ToolsCall(string tool, string args = "{}") =>
        $$$"""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{args}}}}}""";

    private async Task<HttpResponseMessage> PostMcp(string? token, string body, Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        configure?.Invoke(request);
        return await factory.CreateClient().SendAsync(request);
    }

    private static string Token(string scp, string? role = null, string group = ContosoGroup) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = $"https://login.microsoftonline.com/{TenantId}/v2.0",
        Audience = ClientId,
        Subject = new ClaimsIdentity(
            new[] { new Claim("oid", "alice-oid"), new Claim("name", "Alice"), new Claim("scp", scp) }
                .Concat(role is null ? [] : [new Claim("roles", role)])
                .Append(new Claim("groups", group))),
        SigningCredentials = new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256),
    });
}
