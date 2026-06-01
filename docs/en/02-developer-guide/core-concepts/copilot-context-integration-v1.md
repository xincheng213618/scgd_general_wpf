# Copilot Context Integration v1

This document records the first version design boundaries, landed items, and subsequent plans for ColorVision Copilot's evolution from an "independent chat panel" to a "software context service."

## Goal

The current Copilot already has sessions, model configuration, attachments, tool calls, log diagnostics, and a right-side Dock panel, but its understanding of business objects still primarily stays at file paths and user-manually-attached context.

The next phase's goal is not to continue point-enhancing chat capabilities, but to transform Copilot into a public platform capability callable by various modules:

- Business modules do not directly depend on the ColorVision/Copilot directory implementation.
- Copilot can automatically carry structured context of the current software scenario.
- Copilot's entry points expand from the status bar to workflow nodes such as solution tree, images, flows, algorithm results, and exceptions.

## Simple Version Landed This Time

This time, a low-risk, reusable foundation layer was completed first:

1. Public Contract Layer

- Added `ICopilotService`, `ICopilotContextProvider`, and minimal request/context models under UI/ColorVision.Common/Interfaces/Copilot.
- `CopilotPanelService` now serves as the implementation of `ICopilotService` and is exposed to other modules through `CopilotServiceRegistry`.

2. Minimal Context Provider

- Added `CopilotContextRegistry` and `CopilotWorkspaceContextProvider`.
- Current Agent requests automatically attach "current workspace" context, including: solution root directory, current active content, current search roots.
- This step does not directly understand business objects, but first turns "the software's current working surface" into a publicly extensible context entry point.

3. Solution Tree Entry Points

- `FileNode` adds "Ask AI to explain this file" and "Ask AI to diagnose this file/log."
- `FolderNode` adds "Ask AI to summarize this folder."
- These entry points trigger through the public `ICopilotService` rather than directly referencing specific Copilot implementations.

4. Brand Unification

- Status bar entry name adjusted from `GitHub Copilot` to `ColorVision Copilot`.

## Why Stop Here First

This version deliberately does not directly enter the Engine, ImageEditor, Flow, or device service layers, because:

- Once these modules directly reference ColorVision/Copilot, reverse coupling will quickly form.
- What's most lacking right now is not "more chat features," but "context interfaces that can be stably called by business modules."
- First get the public contract, minimal context collection, and high-frequency entry points running, then connecting business objects later will cost less.

## Subsequent Plans

### Phase 1: Context Bridge Extension

Prioritize adding these Providers:

- Current window/current page Provider
- Current selected object Provider
- Current image and ROI Provider
- Current log and feedback package Provider
- Current flow/node/last failure Provider

The acceptance criteria for this phase:

- When users launch Copilot from any key page, they don't need to manually repeat the current object description.
- Agent can see structured application context, not just file paths.

### Phase 2: Scenario Entry Points

Prioritize connecting from these types of entry points:

- Image page right-click: Analyze current image, explain current ROI
- Algorithm result page: Explain this result, why it failed
- Flow: Explain current flow, diagnose last failure
- Device panel: Analyze current device status
- Exception dialog: Hand over to Copilot for analysis

This phase must insist on "calling public interfaces" and not let these modules directly depend on specific Copilot windows or ViewModels.

### Phase 3: Navigation Actions

After explanation/diagnosis stabilizes, introduce low-risk UI actions:

- Open a panel
- Focus a window
- Locate a configuration item
- Open log directory

This phase only does navigation and positioning, not high-risk actions like modifying configurations, controlling devices, or running flows.

### Phase 4: Business Tools

On top of existing SearchFiles, GrepText, ReadLocalFile, GetRecentLog, add read-only business tools:

- `GetActiveWorkspaceContextTool`
- `GetSelectedImageContextTool`
- `GetAlgorithmResultContextTool`
- `GetFlowRunSummaryTool`
- `GetDeviceStatusTool`
- `GetDiagnosticsPackageSummaryTool`

## Relationship with Agent/ReAct Roadmap

Copilot Context Integration v1 addresses "whether it understands the current software scenario," not "whether it's more like a general code agent."

Therefore, subsequent routes should split into two parallel lines:

- Context Integration: Addresses software-internal context and entry points.
- Agent/ReAct: Addresses tool planning, low-cost retrieval, structured execution, and more diagnostic tools.

Both routes are important, but the current priority should be Context Integration.

## Current Boundaries

As of this version:

- Public Copilot contract layer exists.
- Minimal workspace context provider exists.
- Solution tree high-frequency entry points exist.
- Business object context for ImageEditor, Flow, Algorithm, Device is still not connected.
- UI action cards, business tools, and global settings page embedding are still not implemented.

Subsequent design and scheduling recommendations should proceed based on this document as a baseline.