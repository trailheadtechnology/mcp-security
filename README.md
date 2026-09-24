# The S in MCP is for Security
Talk by Jonathan "J." Tower

The Model Context Protocol gives AI models a powerful way to call into your .NET applications through tools and resources. It also creates a new class of security problem: the person using the AI client may end up seeing data or performing actions they would never be allowed to do in your usual UI or APIs.

This session shows how to secure MCP endpoints in .NET with OAuth 2.0 and OpenID Connect so that AI interactions stay within the actual permissions of the current user. We look at how to flow identity and claims into MCP calls, apply scope and role checks, respect tenant boundaries, and prevent over-broad tools that let an agent “wander” into other users' data.

Using practical examples, we walk through securing MCP tools that wrap APIs, databases, and files, and show how to shape them so they only operate on data the user is allowed to access. We also cover auditing and monitoring so you can see what AI-driven traffic is doing in production.

This talk is for developers and architects who want to adopt MCP in .NET without creating a surprise escalation path for their users.

## Schedule Time With Me
https://trailheadtechnology.com/connect/?t=mcp-security
