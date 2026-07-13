---
name: colorvision-flow-diagnostics
description: Diagnose ColorVision workflow, node, device, timeout, and ContinueOnFail problems from live context, recent logs, and relevant project code. Use when the user asks why a ColorVision flow or device task failed, stalled, skipped, or produced an unexpected result.
---

# ColorVision flow diagnostics

Use this workflow only for ColorVision-specific diagnosis. Do not use it for ordinary computer-vision explanations.

1. Restate the observed symptom and distinguish a flow failure from a device-offline, timeout, ignored-failure, or result-parsing condition.
2. Prefer current live context and recent logs. Inspect project files only when the user asks about the implementation or runtime evidence points to a specific node or service.
3. Correlate node state, timeout, `ContinueOnFail`, ignored failures, device connectivity, and downstream result handling. Do not infer success from a node merely continuing.
4. Keep read-only diagnosis separate from changes. Never execute an application mutation or apply a template patch unless the user explicitly requested that change.
5. Report the evidence, the most likely cause, confidence, and the smallest safe next verification step.

Read `references/checklist.md` when the symptom crosses more than one of flow, device, timeout, or result layers.
