# Workflow

This chapter is for daily use and does not explain the FlowEngine architecture itself, but helps you determine when to design workflows and when to execute and debug them.

## What Problems Workflows Solve

In ColorVision, workflows are typically used to chain device actions, template processing, and result output into a repeatable business pipeline.

Common scenarios include:

- Calling multiple devices or services in a fixed sequence
- Reusing a set of inspection or processing steps
- Quickly locating which node is stuck when execution fails
- Converging manual operations into savable, repeatable workflows as much as possible

## How to Read This Chapter

### When Building Workflows

- [Workflow Design](./design.md)

Suitable scenarios:

- Creating a new workflow
- Adjusting node order, connections, or parameters
- Confirming where a certain step should be placed

### When Running or Troubleshooting Workflows

- [Workflow Execution & Debugging](./execution.md)

Suitable scenarios:

- Manually executing existing workflows
- Observing execution status and results
- Locating the first failed node

## Suggested Usage Order

1. First clarify whether you are chaining devices, templates, or data processing steps.
2. Enter [Workflow Design](./design.md) to build a minimal runnable version.
3. Then go to [Workflow Execution & Debugging](./execution.md) to verify whether each step actually executes as expected.
4. If the workflow involves device services, go back and check the corresponding device page rather than just repeatedly retrying in the workflow window.

## Common Issues

### Workflow Can Be Opened but Won't Run

- First, do not modify many nodes simultaneously
- First locate the first failed node
- Then check the device, template, or input data that node depends on

### Workflow Execution Results Do Not Match Expectations

- First confirm whether the correct workflow version is being run
- Then confirm whether node parameters and input objects correspond to the current project
- If results have database storage or export anomalies, cross-check with [Data Management](../data-management/README.md)

### Unsure Whether the Problem is in the Workflow or Device

- First check [Workflow Execution & Debugging](./execution.md)
- Then return to the relevant device topic page to confirm service status and configuration
- Also combine with [Log Viewer](../interface/log-viewer.md) to narrow the scope

## Continue Reading

- [Workflow Design](./design.md)
- [Workflow Execution & Debugging](./execution.md)
- [Device Service Overview](../devices/overview.md)
- [Data Management](../data-management/README.md)

## Notes

- This page only retains the usage entry point for workflows and no longer maintains architecture and API navigation.