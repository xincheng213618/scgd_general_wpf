---
name: colorvision-flow-authoring
description: Inspect and safely edit the active ColorVision Flow graph using runtime node types, stable node and port ids, revision checks, previews, and explicit approval. Use when the user asks to add nodes such as cameras, change node properties, connect existing nodes, inspect topology before an edit, or modify a Flow graph; Chinese intents include 添加节点、创建节点、相机节点、修改属性、修改流程、流程连线.
---

# ColorVision Flow authoring

Use ColorVision's structured Flow tools. Never parse or edit a `.stn` file as text.

## Workflow

1. Call `InspectFlowGraph` to obtain the active graph, stable instance and port ids, and its `revision`.
2. For `add_node`, call `SearchFlowNodeCatalog` with a domain term such as `相机` or `camera`. Use the returned exact `typeKey`; never invent a .NET type name. If multiple types match, ask which behavior or device is intended when the choice changes the result.
3. For `set_property`, use the exact stable node id and writable `propertyName` exposed by the graph and catalog. Pass the value in the node descriptor's string form.
4. For `connect`, use the exact source `out:N` and target `in:N` port ids from the graph. Do not infer ports from display order or names.
5. Call `PreviewFlowPatch` with exactly one operation and the current revision. Describe the validated change, then call `ApplyFlowPatch` only when the request clearly asks for it; wait for explicit approval.
6. Call `InspectFlowGraph` again. Verify that the revision changed and that the node, property, or edge now matches the request.

## Safety boundary

- Do not add or edit nodes while the flow is running.
- Each patch performs exactly one `add_node`, `set_property`, or `connect` operation. It never saves or runs the flow.
- Never change password, token, secret, license, or similar sensitive properties.
- Do not simulate unsupported deletion, disconnection, or batch edits with file or shell tools.
- Treat a revision mismatch as a stale preview: inspect again and create a new preview.
- Use `colorvision-flow-diagnostics` instead when the request is diagnosis without an explicit graph change.
