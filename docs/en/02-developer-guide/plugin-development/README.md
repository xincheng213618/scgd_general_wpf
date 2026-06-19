# Plugin Development Manual

This chapter explains how to develop, load, debug, and package a runtime plugin. Existing plugin capabilities live in [Existing Plugin Capabilities](../../04-api-reference/plugins/README.md); customer project package workflows live in [Project Guide](../../00-projects/README.md).

If you are choosing an existing plugin as a reference, start with [Plugin Capability & Handoff Matrix](../../04-api-reference/plugins/plugin-capability-matrix.md). To confirm that every current plugin has a matching documentation entry, use [Current Plugin Documentation Coverage](../../04-api-reference/plugins/current-plugin-coverage.md). For release or handoff acceptance, use [Existing Plugin Field Acceptance And Handoff Checklist](../../04-api-reference/plugins/plugin-field-acceptance.md).

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

- [Plugin Capability & Handoff Matrix](../../04-api-reference/plugins/plugin-capability-matrix.md): compare current plugin extension points, dependencies, and release risks.
- [Current Plugin Documentation Coverage](../../04-api-reference/plugins/current-plugin-coverage.md): map real `Plugins/` directories to manifests, single-plugin pages, and acceptance entries.
- [Existing Plugin Field Acceptance And Handoff Checklist](../../04-api-reference/plugins/plugin-field-acceptance.md): accept entries, smoke tests, external dependencies, and rollback material for current plugins.
- [Conoscope](../../04-api-reference/plugins/standard-plugins/conoscope.md): image viewing, focus points, color gamut, and contrast analysis.
- [Spectrum](../../04-api-reference/plugins/standard-plugins/spectrum.md): spectrometer connection, calibration, measurement, and result storage.
- [SystemMonitor](../../04-api-reference/plugins/standard-plugins/system-monitor.md): system performance and status monitoring.
- [WindowsServicePlugin](../../04-api-reference/plugins/standard-plugins/windows-service.md): Windows service installation and runtime configuration.

## Notes

- This page provides the development entry and does not replace current plugin capability pages.
- Adding, deleting, or restoring a plugin must update [Current Plugin Documentation Coverage](../../04-api-reference/plugins/current-plugin-coverage.md), the matrix, acceptance checklist, and navigation.
- If a plugin depends on project-level custom logic, also check the corresponding implementation under `Projects/`.
