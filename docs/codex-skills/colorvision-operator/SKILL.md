---
name: colorvision-operator
description: "Use when: operating a running ColorVision WPF app through the local ColorVision MCP server for diagnostics, navigation, logs, workspace context, template context, flow context, or code inspection."
---

# ColorVision Operator

Use this skill when you are connected to the `colorvision` MCP server and need to inspect or safely operate a running ColorVision app.

## First Moves

1. Call `get_server_status` first.
2. Read `colorvision://live-context/current` next.
3. Read `colorvision://workspace/current` if the task mentions code, files, solution nodes, or project structure.
4. Read logs, template, or flow resources only when the user's request makes them relevant.

## Diagnostic Workflow

- For app state questions, start from live context and `get_runtime_environment_summary`.
- For recent failures, read `colorvision://logs/recent`, then call `get_recent_log` with a narrow query if needed.
- For template questions, read `colorvision://template/current` before searching code.
- For flow questions, read `colorvision://flow/current`; treat it as read-only and never start, stop, or run a flow.
- For product behavior questions, use `search_docs` before searching source code.
- For code questions, use `search_files`, `grep_text`, and then `read_allowed_file` on specific text files under allowed roots.

## Safe Navigation

Use `open_panel` or `execute_menu` only for low-risk navigation, such as opening Copilot, settings, logs, docs, or diagnostic panels.

## Hard Boundaries

Refuse or ask the user before any request that would:

- control devices
- execute, stop, or modify flows
- mutate configuration
- delete files
- run shell commands
- read arbitrary files outside the allowed ColorVision workspace roots
- upload image pixels or binary payloads

When a user asks for a high-risk action, explain that the current MCP integration is read-only plus low-risk navigation, then offer a safe inspection path.