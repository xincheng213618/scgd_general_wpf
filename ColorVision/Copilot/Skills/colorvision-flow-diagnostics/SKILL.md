---
name: colorvision-flow-diagnostics
description: Diagnose ColorVision workflow, node, device, timeout, ContinueOnFail, and slow-run problems using current context or exact retained run and incident evidence. Use for failures, stalls, skips, unexpected results, historical runs, and retained configuration comparisons; Chinese intents include 流程、节点、设备、超时、失败、卡住、跳过、结果异常、慢运行、历史运行、流程事故、配置差异.
---

# ColorVision flow diagnostics

Use this workflow only for ColorVision-specific diagnosis. Do not use it for ordinary computer-vision explanations.

1. Restate the observed symptom and distinguish a flow failure from a device-offline, timeout, ignored-failure, or result-parsing condition. Establish whether the question concerns the current UI or a retained run or incident.
2. Use live context and recent logs as current-state clues. For recorded Flow questions, use exact IDs supplied by the user or formal context; when missing, discover runs with `ListFlowRuns`, including slow or normal runs without incidents, or discover incidents with `ListFlowIncidents`. Read an incident through `InspectFlowIncident` before following its associated run ID. Never infer IDs from an SN, node label, template name, or current canvas.
3. Use the reference checklist to select only the required run events, node attempts, history, retained template, or comparison evidence. Current `InspectFlowGraph` evidence describes the active canvas, not a historical run. Correlate node state, timeout, `ContinueOnFail`, ignored failures, device connectivity, and downstream result handling; continuation alone does not prove success. Inspect project code only for implementation questions or a specific evidence-backed node or service.
4. Keep diagnosis read-only: do not control hardware, rerun a flow, acknowledge or resolve an incident, or change templates. An explicitly requested change requires its separate protected workflow and approvals.
5. Report observed evidence, exact identifiers, coverage limits, confidence, and the smallest safe next verification step. State that the cause remains unresolved when the evidence does not establish one. Do not interpret an empty incident list as proof of success, or a bounded history sample as a population success rate or customer PASS/FAIL.

Read `references/checklist.md` for retained run, incident, or template questions, and when a symptom crosses more than one of flow, device, timeout, or result layers.
