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

namespace TodoAPI.Tests;

/// <summary>
/// Progressive auth, without Entra: tokens are signed locally with a test key that replaces Entra's signing keys.
/// Values match appsettings.json so the server's issuer/audience checks still run for real.
/// </summary>
public class StepUpTests(StepUpTests.Factory factory) : IClassFixture<StepUpTests.Factory>
{
    private const string TenantId = "71fba380-7938-4456-9389-e94c9aefcd1f";
    private const string ClientId = "07f59dcc-1a3f-456f-94a9-f79da75c8326";
    private const string ReadScope = $"api://{ClientId}/Todos.Read";
    private const string WriteScope = $"api://{ClientId}/Todos.Write";

    private static readonly SymmetricSecurityKey TestKey = new(Encoding.UTF8.GetBytes(new string('k', 64)));

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
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
        var response = await PostMcp(Token("Todos.Read"), ToolsCall("get_todos"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Read_only_token_calling_write_tool_gets_step_up_challenge()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsCall("add_todo", """{"title":"Buy milk"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"insufficient_scope\"", challenge);
        Assert.Contains($"scope=\"{ReadScope} {WriteScope}\"", challenge); // keeps read, adds write
        Assert.Contains("resource_metadata=", challenge);
    }

    [Fact]
    public async Task Read_write_token_can_call_write_tool()
    {
        var response = await PostMcp(Token("Todos.Read Todos.Write"), ToolsCall("add_todo", """{"title":"Buy milk"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Buy milk", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lying_Mcp_Name_header_does_not_bypass_step_up()
    {
        var response = await PostMcp(Token("Todos.Read"), ToolsCall("add_todo", """{"title":"Sneaky"}"""),
            request =>
            {
                request.Headers.Add("MCP-Protocol-Version", "2025-11-25"); // older version: SDK won't cross-check headers
                request.Headers.Add("Mcp-Method", "tools/call");
                request.Headers.Add("Mcp-Name", "get_todos");
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private static string Token(string scp) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = $"https://login.microsoftonline.com/{TenantId}/v2.0",
        Audience = ClientId,
        Subject = new ClaimsIdentity([new Claim("oid", "alice-oid"), new Claim("name", "Alice"), new Claim("scp", scp)]),
        SigningCredentials = new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256),
    });
}
