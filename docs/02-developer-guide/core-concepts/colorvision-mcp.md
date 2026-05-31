# ColorVision MCP

ColorVision MCP exposes a local Model Context Protocol endpoint so Codex can inspect and safely operate a running ColorVision WPF session for diagnostics, navigation, and context inspection.

The endpoint is local-only, disabled by default, bound to loopback, and protected by a bearer token. It deliberately reuses `ColorVision/Copilot/Capabilities/` so the in-app Agent and external MCP clients share search, grep, file read, docs search, recent log, menu, theme, and language behavior.

## Current Status

Implemented:

- Local HTTP JSON-RPC MCP endpoint at `http://127.0.0.1:38473/mcp` by default.
- Bearer token authentication, generated and stored through Copilot settings.
- Tool listing, tool calls, resource listing, and resource reads.
- Health and diagnostic tools for server status, enabled tools, audit logs, last tool error, and runtime environment summaries.
- Read-only resources for live context, workspace, logs, template, flow, and MCP audit log.
- Business context capture for template JSON editor, flow and selected flow node state, recent flow failure clues, image editor metadata without pixels, and selected solution file/folder entries.
- Audit logging for MCP tool calls with timestamp, tool name, redacted argument summary, success/failure, duration, error message, and caller/source when available.
- Offline smoke tests for auth, disabled server behavior, server status, resources, allowed-root file reads, file search, grep, audit records, and low-risk action routing.

Intentionally not supported:

- device control
- flow execution or flow stop/start
- config mutation
- file deletion
- arbitrary shell or process execution
- arbitrary file read outside normalized ColorVision workspace roots
- binary image upload through context snapshots

## Enable MCP

1. Open ColorVision.
2. Open Copilot settings.
3. Enable `Local MCP`.
4. Confirm or edit the local port.
5. Copy the endpoint and bearer token.
6. Save settings. The local server applies the setting immediately.

Default endpoint:

```text
http://127.0.0.1:38473/mcp
```

Use `Regenerate token` if the token may have been copied outside the local machine.

## Codex Config

For Codex builds that support streamable HTTP MCP servers, add a server entry like this to `config.toml`:

```toml
[mcp_servers.colorvision]
url = "http://127.0.0.1:38473/mcp"
headers = { Authorization = "Bearer <token from ColorVision settings>" }
```

If your Codex build requires an explicit transport field, use:

```toml
[mcp_servers.colorvision]
transport = "streamable_http"
url = "http://127.0.0.1:38473/mcp"
headers = { Authorization = "Bearer <token from ColorVision settings>" }
```

Restart the Codex session after editing the config.

## Operator Workflow

Codex should use this conservative order:

1. Call `get_server_status` first.
2. Read `colorvision://live-context/current`.
3. Read `colorvision://workspace/current` if code or file context is needed.
4. Read logs, template, or flow resources only when they are relevant to the user request.
5. Use `search_docs` for product behavior and user workflow questions.
6. Use `search_files`, `grep_text`, and `read_allowed_file` for code or configuration questions.
7. Use `open_panel` and `execute_menu` only for low-risk navigation.
8. Refuse or ask the user before any action that would control devices, execute flows, mutate config, delete files, read arbitrary files, or run shell commands.

## Tools

Health and diagnostics:

- `get_server_status`: reports ColorVision process, MCP enabled state, listener state, endpoint, auth state for the current request, caller/source, and safety boundary.
- `get_enabled_tools`: lists the exposed MCP tools.
- `get_audit_log`: returns recent MCP tool call audit entries.
- `get_last_tool_error`: returns the latest failed MCP tool call.
- `get_runtime_environment_summary`: summarizes workspace, live context, flow availability, selected flow nodes, recent log availability, and audit count.

Context and search:

- `get_live_context`: current published Copilot live context.
- `get_workspace_context`: solution directory, active document, and allowed search roots.
- `get_recent_log`: recent application log lines, optionally filtered.
- `search_docs`: published ColorVision documentation search.
- `search_files`: file name/path search under allowed roots.
- `grep_text`: literal text search under allowed roots.
- `read_allowed_file`: text file read under allowed roots, with optional line range.
- `list_allowed_directory`: directory listing under allowed roots.
- `get_active_template_context`: active template editor context when available.
- `get_flow_summary`: read-only active flow summary.

Low-risk navigation:

- `open_panel`: opens a ColorVision panel such as Copilot.
- `execute_menu`: executes a visible main-window menu command by name or path.
- `set_theme`: switches the UI theme.
- `set_language`: switches UI language through the existing app flow.

## Resources

MCP resources are cheap, read-only, and safe to fetch repeatedly:

- `colorvision://live-context/current`
- `colorvision://workspace/current`
- `colorvision://logs/recent`
- `colorvision://template/current`
- `colorvision://flow/current`
- `colorvision://mcp/audit-log`

Resources return plain text snapshots. They never trigger device operations, flow execution, file mutation, or shell commands.

## File Boundary

MCP file tools are constrained to normalized ColorVision workspace roots. Relative paths resolve against the first allowed root. `read_allowed_file` also requires a text-like extension and rejects paths outside the allowed root set.

## Validation

Focused tests live in `Test/ColorVision.UI.Tests/CopilotMcpTests.cs` and `Test/ColorVision.UI.Tests/CopilotCapabilitiesTests.cs`.

Use these commands during development:

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "CopilotCapabilitiesTests|CopilotMcpTests" -v minimal
dotnet build ColorVision/ColorVision.csproj -v minimal
```

The full UI test suite currently includes unrelated WPF STA-sensitive tests; failures there should be reported separately from MCP regressions.