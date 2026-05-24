# Architecture Design

This chapter only retains the main reading path for the current system design. Historical proposals, split drafts, and one-time discussion documents are still kept in the directory but are no longer used as default entry points.

## Main Reading Path

1. [System Architecture Overview](./overview/system-overview.md)
2. [Architecture Runtime](./overview/runtime.md)
3. [Component Interactions](./overview/component-interactions.md)
4. [FlowEngineLib Architecture](./components/engine/flow-engine.md)
5. [Templates Architecture Design](./components/templates/design.md)
6. [Security Overview](./security/overview.md)
7. [RBAC Model](./security/rbac.md)

## Directory Description

- `overview/` focuses on system-level perspectives, such as startup, runtime, and component relationships.
- `components/engine/` focuses on flow engine and execution model.
- `components/templates/` focuses on template system design and status analysis.
- `security/` focuses on permission models and security boundaries.

## Recommended Reading Order

- When encountering the system for the first time, read in the order: "System Overview → Runtime → Component Interactions."
- When you need to modify flows or templates, then enter the topic pages under `components/`.
- When you need interface and type details, go to [API Reference](../04-api-reference/README.md).

## Supplementary Reading

- [Templates Module Analysis](./components/templates/analysis.md): Suitable for revisiting directory evolution, registration boundaries, and current constraints after understanding the template design mainline.

## Historical Material Notes

- Documents in this directory starting with `ColorVision.Engine-Refactoring-` belong to historical design materials, used for tracing ideas, and are no longer considered current default solutions.
- If historical solutions conflict with current code implementation, the code and current module documentation take precedence.