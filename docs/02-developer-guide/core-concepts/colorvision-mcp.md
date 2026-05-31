# ColorVision MCP

ColorVision MCP exposes a local Model Context Protocol endpoint so Codex can inspect and safely operate a running ColorVision WPF session for diagnostics, navigation, and context inspection.

The endpoint is local-only, disabled by default, bound to loopback, and protected by a bearer token. It deliberately reuses `ColorVision/Copilot/Capabilities/` so the in-app Agent and external MCP clients share search, grep, file read, docs search, recent log, menu, theme, and language behavior.

MCP v3 adds user-confirmed actions and preview-only business operations. Every exposed MCP tool is categorized, has a risk level, and is audited when called. The operation model remains conservative: inspect first, dry-run actions when possible, execute low-risk UI operations directly, and require the user to approve confirmation-required actions inside ColorVision before `confirm_action` can execute them. Device control, flow execution, shell execution, file deletion, and arbitrary file reads remain outside the MCP surface.

## Current Status

Implemented:

- Local HTTP JSON-RPC MCP endpoint at `http://127.0.0.1:38473/mcp` by default.
- Bearer token authentication, generated and stored through Copilot settings.
- Tool listing, tool calls, resource listing, and resource reads.
- Health and diagnostic tools for server status, enabled tools, audit logs, last tool error, and runtime environment summaries.
- `tools/list` metadata with category, risk level, usage example, and MCP annotations.
- `get_enabled_tools` grouped by `status`, `context`, `search`, `file`, `app-control`, and `audit`.
- Audit filtering by tool name, action id, and failed-only mode.
- Read-only resources for live context, workspace, logs, template, flow, and MCP audit log.
- Business context capture for template JSON editor, flow and selected flow node state, recent flow failure clues, image editor metadata without pixels, and selected solution file/folder entries.
- Low-risk app-control helpers for opening known panels, dry-running menu resolution, executing low-risk menus, and refusing confirmation-required menu actions by default.
- Confirmable actions with `action_id`, title, description, risk level, source tool, redacted argument summary, creation time, and expiry time. Pending actions expire after a short lifetime, currently five minutes.
- Pending Actions UI in the Copilot panel with Approve and Reject buttons.
- `confirm_action` for executing only a previously approved, non-expired, argument-matched confirmation-required action.
- Preview-only `preview_template_patch` and `preview_flow_action` tools.
- Audit logging for MCP tool calls with timestamp, tool name, redacted argument summary, success/failure, duration, error message, and caller/source when available.
- Audit logging for `action_created`, `action_approved`, `action_rejected`, `action_expired`, and `action_executed`.
- Offline smoke tests for auth, disabled server behavior, server status, resources, allowed-root file reads, file search, grep, audit records, and low-risk action routing.

Intentionally not supported:

- device control
- flow execution or flow stop/start
- config mutation
- template patch apply/save through MCP v3
- file deletion
- arbitrary shell or process execution
- shell, cmd, PowerShell, batch, Python, or arbitrary command execution tools
- arbitrary file read outside normalized ColorVision workspace roots
- binary image upload through context snapshots

Codex may have its own local command-line tools depending on how the user runs Codex. Those tools are separate from ColorVision MCP. ColorVision MCP does not expose shell/cmd/PowerShell execution, and agents should not describe Codex shell access as a ColorVision MCP capability.

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
bearer_token_env_var = "COLORVISION_MCP_TOKEN"
```

If your Codex build requires an explicit transport field, use:

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

Restart the Codex session after editing the config or changing the environment variable.

## Operator Workflow

Codex should use this conservative order:

1. Call `get_server_status` first.
2. Read `colorvision://live-context/current`.
3. Read `colorvision://workspace/current` if code or file context is needed.
4. Read logs, template, or flow resources only when they are relevant to the user request.
5. Use `search_docs` for product behavior and user workflow questions.
6. Use `search_files`, `grep_text`, and `read_allowed_file` for code or configuration questions.
7. Use `open_panel` for known low-risk panels: `copilot`, `log`, `config`, `solution`, `template`, `flow`, or `device`.
8. Use `execute_menu` with `dry_run: true` before asking for execution.
9. Execute directly only when the returned risk is `low-risk-action`.
10. For `confirmation-required`, expect an `action_id`, ask the user to approve it in ColorVision's Copilot Pending Actions area, then call `confirm_action` with the same `tool_name` and `arguments_summary` returned by the creation response.
11. Refuse any action that would control devices, execute flows, mutate config without confirmation, delete files, read arbitrary files, or run shell commands.

Recommended menu flow:

```json
{ "query": "View > Copilot", "dry_run": true }
```

If the response identifies the expected menu and `risk_level=low-risk-action`, a client may call:

```json
{ "query": "View > Copilot", "dry_run": false }
```

If the response says `confirmation_required`, it returns an `action_id` and redacted `arguments_summary`. The user must approve the pending action in ColorVision before the client calls:

```json
{
	"action_id": "a1b2c3d4e5f6",
	"tool_name": "execute_menu",
	"arguments_summary": "query=Tools > Update, dry_run=False"
}
```

`confirm_action` checks the action id, expiry time, risk level, tool name, and argument summary. Rejected, expired, already executed, or mismatched actions return a clear status and do not execute.

## Tools

| Tool | Category | Risk | Example | Notes |
| --- | --- | --- | --- | --- |
| `get_server_status` | `status` | `read-only` | `{}` | MCP listener, endpoint, auth, caller/source, pending action count, and safety boundary. |
| `get_enabled_tools` | `status` | `read-only` | `{}` | Human-readable grouped tool catalog with examples. |
| `get_runtime_environment_summary` | `status` | `read-only` | `{}` | Version, process/start time, dirs, cultures, theme, workspace, flow, log, and audit summary with secret redaction. |
| `get_audit_log` | `audit` | `read-only` | `{ "max_entries": 20, "action_id": "a1b2c3d4e5f6" }` | Recent tool calls and action lifecycle events, optionally filtered by tool, action id, or failed-only. |
| `get_last_tool_error` | `audit` | `read-only` | `{}` | Latest failed tool call, if any. |
| `get_live_context` | `context` | `read-only` | `{}` | Current in-app Copilot context snapshot. |
| `get_workspace_context` | `context` | `read-only` | `{}` | Solution directory, active document, and allowed roots. |
| `get_active_template_context` | `context` | `read-only` | `{}` | Template editor metadata, template type/name, and key JSON parameters when available. |
| `get_flow_summary` | `context` | `read-only` | `{}` | Active flow summary, selected nodes, recent run/error clues; never executes flows. |
| `preview_template_patch` | `context` | `read-only` | `{ "template_identifier": "Default", "proposed_changes": { "Exposure": 12 } }` | Validates current/proposed JSON and reports changed fields. It does not save and refuses secret/token/password/api key fields. |
| `preview_flow_action` | `context` | `read-only` | `{ "action": "inspect_node_errors", "node_name": "Camera" }` | Previews select/open/inspect flow actions. It refuses start/stop/run flow requests. |
| `get_recent_log` | `search` | `read-only` | `{ "max_lines": 80, "filter": "error" }` | Recent app log lines with optional literal filter. |
| `search_docs` | `search` | `read-only` | `{ "query": "MCP", "max_results": 5 }` | ColorVision docs search. |
| `search_files` | `search` | `read-only` | `{ "query": "Copilot", "max_results": 20 }` | File/path search under allowed roots. |
| `grep_text` | `search` | `read-only` | `{ "query": "MCP", "max_results": 20 }` | Literal text search under allowed roots. |
| `read_allowed_file` | `file` | `read-only` | `{ "path": "README.md", "start_line": 1, "max_lines": 80 }` | Text file reads only inside normalized allowed roots. |
| `list_allowed_directory` | `file` | `read-only` | `{ "path": "docs", "max_entries": 80 }` | Directory listing only inside allowed roots. |
| `open_panel` | `app-control` | `low-risk-action` | `{ "panel": "flow" }` | Opens supported aliases: `copilot`, `log`, `config`, `solution`, `template`, `flow`, `device`; unknown aliases fail with the supported list. |
| `execute_menu` | `app-control` | `confirmation-required` | `{ "query": "View > Copilot", "dry_run": true }` | Defaults to `dry_run: true`; low-risk menus may run with `dry_run: false`; confirmation-required menus create a pending action and return `action_id`. |
| `confirm_action` | `app-control` | `confirmation-required` | `{ "action_id": "a1b2c3d4e5f6", "tool_name": "execute_menu", "arguments_summary": "query=Tools > Update, dry_run=False" }` | Executes only an approved, non-expired, argument-matched pending action. |
| `set_theme` | `app-control` | `low-risk-action` | `{ "theme": "dark" }` | Switches UI theme through existing app services. |
| `set_language` | `app-control` | `confirmation-required` | `{ "language": "en-US" }` | Creates a pending action before invoking the existing confirmation/restart language flow. |

Health and diagnostics:

- `get_server_status` (`status`, `read-only`): reports ColorVision process, MCP enabled state, listener state, endpoint, auth state for the current request, caller/source, pending action count, and safety boundary.
- `get_enabled_tools` (`status`, `read-only`): lists exposed MCP tools grouped by category with risk and usage examples.
- `get_runtime_environment_summary` (`status`, `read-only`): summarizes version, process/start time, config/log directories, language, theme, MCP listener status, workspace, live context, flow availability, recent log availability, and audit count. It must not include bearer tokens or API keys.
- `get_audit_log` (`audit`, `read-only`): returns recent MCP tool call and action lifecycle audit entries. Optional arguments: `max_entries`, `tool`, `action_id`, `failed_only`.
- `get_last_tool_error` (`audit`, `read-only`): returns the latest failed MCP tool call.

Context and search:

- `get_live_context` (`context`, `read-only`): current published Copilot live context.
- `get_workspace_context` (`context`, `read-only`): solution directory, active document, and allowed search roots.
- `get_active_template_context` (`context`, `read-only`): active template editor context when available, including template editor metadata, template type/name, and parsed top-level JSON keys/key parameters when possible.
- `get_flow_summary` (`context`, `read-only`): read-only active flow summary, selected nodes, and recent run/error clues. It never starts, stops, reruns, or edits a flow.
- `preview_template_patch` (`context`, `read-only`): validates proposed top-level template JSON changes and reports a diff summary. It never saves and refuses secret/token/password/API key fields.
- `preview_flow_action` (`context`, `read-only`): previews select node, open node property, and inspect node errors. It refuses start/stop/run flow requests and never executes a flow.
- `get_recent_log` (`search`, `read-only`): recent application log lines, optionally filtered.
- `search_docs` (`search`, `read-only`): published ColorVision documentation search.
- `search_files` (`search`, `read-only`): file name/path search under allowed roots.
- `grep_text` (`search`, `read-only`): literal text search under allowed roots.
- `read_allowed_file` (`file`, `read-only`): text file read under allowed roots, with optional line range.
- `list_allowed_directory` (`file`, `read-only`): directory listing under allowed roots.

App-control:

- `open_panel` (`app-control`, `low-risk-action`): opens a known ColorVision panel alias such as `copilot`, `log`, `config`, `solution`, `template`, `flow`, or `device`. Unknown aliases return the supported alias list.
- `execute_menu` (`app-control`, `confirmation-required`): resolves a visible main-window menu command by name or path. It defaults to `dry_run: true`. Low-risk matches may execute when `dry_run` is false; confirmation-required matches create pending actions and return `action_id`.
- `confirm_action` (`app-control`, `confirmation-required`): executes only after the user approves the pending action in ColorVision. The call must provide matching `action_id`, `tool_name`, and `arguments_summary`.
- `set_theme` (`app-control`, `low-risk-action`): switches the UI theme.
- `set_language` (`app-control`, `confirmation-required`): switches UI language through the existing app confirmation/restart flow.

Risk levels:

- `read-only`: returns state, context, logs, docs, or files within allowed roots; no app state changes.
- `low-risk-action`: UI navigation or easily reversible local UI state, such as opening a panel or changing theme.
- `confirmation-required`: operation could affect application state, files, updates, restart, or another higher-risk app workflow. MCP creates a short-lived pending action, displays it in ColorVision, and executes it only after user approval and a matching `confirm_action` call. Flow execution and device control are still not implemented as confirmable operations in MCP v3.

## Confirmation Flow

Confirmable actions are in-memory and short-lived. They include:

- `action_id`
- `title`
- `description`
- `risk_level`
- `tool_name`
- redacted `arguments_summary`
- `created_at`
- `expires_at`

Sensitive argument values such as tokens, API keys, passwords, authorization headers, and bearer secrets are redacted and are not stored in action summaries. If a tool call contains a sensitive value that would need to be retained for execution, MCP refuses to create the pending action.

Lifecycle audit events:

- `action_created`
- `action_approved`
- `action_rejected`
- `action_expired`
- `action_executed`

The user approves or rejects pending actions in the Copilot panel. Approval alone does not execute the action; it only authorizes the next matching `confirm_action` call. Rejected, expired, repeated, or argument-mismatched calls fail without executing.

## In-App Agent Tools

The built-in ColorVision Agent uses the same capability layer but is not identical to the MCP surface. Current in-app Agent tools include:

- `SearchFiles`
- `GrepText`
- `GetRecentLog`
- `FetchUrl`
- `ReadLocalFile`
- `ListDirectory`
- `SearchDocs`
- `ExecuteMenu`
- `SetTheme`
- `SetLanguage`
- `ReadAttachedFile`

The external MCP surface intentionally excludes `FetchUrl` and attached-file handling, and keeps file reads constrained to ColorVision workspace roots.

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

Use these minimal commands during development:

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "CopilotCapabilitiesTests|CopilotMcpTests" -v minimal
dotnet build ColorVision/ColorVision.csproj -v minimal
```

The full UI test suite should also complete with the WPF STA-sensitive window tests skipped:

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -v minimal
```

## Troubleshooting

- Not saved: after changing the port or token in ColorVision, click `Save` so the MCP server applies the new settings.
- Port unavailable: choose another local port if the status shows `Port unavailable`, then save again.
- Token mismatch: copy the current token from ColorVision settings, update `COLORVISION_MCP_TOKEN`, and restart Codex.
- Codex needs restart: Codex usually reads MCP server config and environment variables when the session starts.
- Disabled server: enable `Enable local MCP server` in Copilot settings and save.
- Wrong endpoint: keep the Codex URL aligned with the ColorVision `Endpoint` field.