# Plugin Development Overview

This chapter is intended for developers who need to extend ColorVision functionality, prioritizing plugin development paths that are still effective.

## Plugin Location in the Repository

- Runtime plugin source code is located in `Plugins/`
- Plugins are discovered and loaded by the main application at runtime
- If a plugin has a UI, it typically needs to enable WPF and follow the main application's interface conventions

## Shortest Path to Develop a Plugin

1. First read [Extensibility Overview](../core-concepts/extensibility.md)
2. Then read [Plugin Development Getting Started](./getting-started.md)
3. When you need to understand the runtime phase, read [Plugin Lifecycle](./lifecycle.md)

## Currently Recommended Conventions

- Keep the target framework aligned with the main repository's Windows desktop direction
- Enable WPF when a UI is needed
- After building, copy output to `Plugins/<Name>/` under the main application output directory
- Prioritize referencing the organization of existing standard plugins rather than creating a new set of conventions

## Suggested Existing Plugins to Reference

- [Pattern Plugin](../../04-api-reference/plugins/standard-plugins/pattern.md)
- [Spectrum Plugin](../../04-api-reference/plugins/standard-plugins/spectrum.md)
- [SystemMonitor Plugin](../../04-api-reference/plugins/standard-plugins/system-monitor.md)

## Notes

- This page only provides an entry point and does not expand on overly detailed historical design details.
- If a plugin depends on project-level custom logic, also check the corresponding implementation under `Projects/`.