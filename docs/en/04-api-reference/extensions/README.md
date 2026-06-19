# Extension Points Overview

This chapter only retains extension point topics that can currently be directly matched against code, no longer maintaining the old-style "overview of all extension mechanisms" summary table.

## Current Coverage

Currently this branch only consolidates one category of stable topics:

- [FlowEngineLib Node Extensions](./flow-node.md)

This means the current `extensions/` is not a complete extension encyclopedia, but a very narrow "organized extension point entry."

## First, Clarify the Boundaries

- Plugin discovery, loading, and deployment do not belong here — see [Plugins and Status](../plugins/README.md) and [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md).
- Algorithm templates and flow templates do not belong here — see [Algorithms and Templates Overview](../algorithms/README.md).
- Dependencies between runtime modules are also not expanded here — return to [Architecture Design](../../03-architecture/README.md).

## How to Use This Chapter

1. First confirm whether you are extending "Flow nodes" or "plugins/templates/services."
2. If it is Flow nodes, then enter [FlowEngineLib Node Extensions](./flow-node.md).
3. If the question leans more toward runtime execution chains, read together with [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md).

## Why There Is Only One Page Here

- There are not many extension points currently organized into stable topics in the repository.
- Rather than continuing to maintain a superficially complete but quickly outdated extension directory, it is better to only retain entry points that can be directly verified against code.

## Continue Reading

- [Module Handbook Overview](../README.md)
- [Plugins and Status](../plugins/README.md)
- [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md)
