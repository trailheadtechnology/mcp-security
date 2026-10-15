using System.Security.Claims;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace TodoAPI.Auth;

/// <summary>
/// One line per tool call: who (user and agent), what (tool + arguments), decision (allowed/denied, and why).
/// Writes straight to the console so it shows in the server terminal; in production, send it to your log pipeline.
/// Calls to tools hidden by [Authorize] are rejected before this filter runs, so they don't show here.
/// </summary>
public static class AuditLog
{
    public static IMcpServerBuilder WithAuditLog(this IMcpServerBuilder builder) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, ct) =>
        {
            var who = $"{Name(context.User)} via {context.Server.ClientInfo?.Name ?? "unknown agent"}";
            var what = $"{context.Params?.Name} {JsonSerializer.Serialize(context.Params?.Arguments)}";
            try
            {
                var result = await next(context, ct);
                Write(result.IsError == true ? $"DENIED  {ErrorText(result)}" : "ALLOWED", who, what);
                return result;
            }
            catch (Exception ex)
            {
                Write($"DENIED  {ex.Message}", who, what);
                throw;
            }
        }));

    private static string Name(ClaimsPrincipal? user) =>
        user?.FindFirst("name")?.Value ?? user?.FindFirst("preferred_username")?.Value ?? "anonymous";

    private static string ErrorText(CallToolResult result) =>
        string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static void Write(string decision, string who, string what) =>
        Console.WriteLine($"AUDIT {DateTime.Now:HH:mm:ss} {decision} | {who} | {what}");
}
