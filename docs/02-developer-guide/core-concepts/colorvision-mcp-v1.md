# ColorVision MCP v1

ColorVision MCP v1 exposes a local Model Context Protocol endpoint so an external coding agent such as Codex can inspect and safely operate the running WPF application.

The endpoint is designed for local development and diagnostics only. It is disabled by default, binds to loopback, requires a bearer token, and routes tool behavior through the same Copilot capability services used by the in-app Agent.

## Architecture

MCP is layered on top of the existing ColorVision Copilot capability model:

```text
External Codex
  -> http://127.0.0.1:38473/mcp
  -> CopilotMcpServer / CopilotMcpRequestHandler
  -> CopilotMcpToolDispatcher
  -> ColorVision/Copilot/Capabilities
  -> existing app services, context registry, docs index, logs, menus, theme, language
```

The important boundary is that Agent tools and MCP tools share capability services. Search, grep, file read, docs search, recent log, menu execution, theme, and language behavior should be implemented once under `ColorVision/Copilot/Capabilities/`, with Agent and MCP acting as adapters.

## Enable MCP

1. Open ColorVision.
2. Open the Copilot settings window.
3. In the `Local MCP` section, enable `Enable local MCP server`.
4. Copy the `Endpoint` and `Bearer token` values.
5. Save settings. The server starts or stops immediately from the saved configuration.

Default endpoint:

```text
http://127.0.0.1:38473/mcp
```

The token is generated automatically. Use `Regenerate token` if a token may have been shared outside the local machine.

## Codex Config Example

For Codex builds that support streamable HTTP MCP servers, add an entry like this to `config.toml`:

```toml
[mcp_servers.colorvision]
url = "http://127.0.0.1:38473/mcp"
bearer_token_env_var = "COLORVISION_MCP_TOKEN"
```

If your Codex build uses an explicit transport field, keep the same URL and set the transport to streamable HTTP:

```toml
[mcp_servers.colorvision]
transport = "streamable_http"
url = "http://127.0.0.1:38473/mcp"
bearer_token_env_var = "COLORVISION_MCP_TOKEN"
```

Set the token in PowerShell before starting Codex:

```powershell
$env:COLORVISION_MCP_TOKEN = "<token from ColorVision settings>"
```

For a persistent user-level environment variable:

```powershell
[Environment]::SetEnvironmentVariable("COLORVISION_MCP_TOKEN", "<token from ColorVision settings>", "User")
```

Restart the Codex session after editing the config or changing the environment variable so it reloads MCP servers.

## Tools

MCP v1 exposes these tools:

- `get_live_context`: current published ColorVision Copilot live context.
- `get_workspace_context`: solution directory, active document, and allowed search roots.
- `get_recent_log`: recent application log lines, optionally filtered.
- `search_docs`: published ColorVision documentation search.
- `search_files`: file-name/path search under allowed workspace roots.
- `grep_text`: literal text search under allowed workspace roots.
- `read_allowed_file`: text file read under allowed roots, with optional line range.
- `list_allowed_directory`: directory listing under allowed roots.
- `get_active_template_context`: active template editor context when available.
- `get_flow_summary`: read-only active flow summary.
- `open_panel`: opens a low-risk ColorVision panel.
- `execute_menu`: executes a visible main-window menu command by name or path.
- `set_theme`: switches the ColorVision UI theme.
- `set_language`: switches UI language through the existing language-change flow.

## Safety Boundaries

MCP v1 intentionally does not expose:

- shell or process execution
- arbitrary file system access
- device control
- flow start/stop/run commands
- database mutation tools
- unmanaged/native DLL calls outside existing app services

File tools are constrained to normalized ColorVision search roots. `read_allowed_file` also requires a text-like extension and rejects paths outside the allowed root set.

Menu/theme/language operations reuse the same guarded capability logic as the in-app Agent. Flow access is read-only and returns a snapshot; it never calls run or stop operations.

## Auditing

Every MCP tool call is logged through the `ColorVision.Copilot.McpAudit` logger with tool name, sanitized argument summary, success state, elapsed time, and result message.

## Test Coverage

Pure capability tests live in `Test/ColorVision.UI.Tests/CopilotCapabilitiesTests.cs`.

Offline MCP request/dispatcher tests live in `Test/ColorVision.UI.Tests/CopilotMcpTests.cs` and cover authorization, disabled-server behavior, tool listing, allowed-root search/grep/read, and outside-root rejection.

Minimal validation commands:

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "CopilotCapabilitiesTests|CopilotMcpTests" -v minimal
dotnet build ColorVision/ColorVision.csproj -v minimal
```

Troubleshooting checklist:

- Not saved: save Copilot settings after changing the MCP port or token.
- Port unavailable: pick a different local port and save again.
- Token mismatch: copy the current token, update `COLORVISION_MCP_TOKEN`, and restart Codex.
- Codex needs restart: restart Codex after changing `config.toml` or environment variables.
- Disabled server: enable `Enable local MCP server` and save.