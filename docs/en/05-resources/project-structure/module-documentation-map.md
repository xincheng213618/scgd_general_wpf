# Module and Documentation Map

This document only retains the current repository structure and still valid documentation entry points, used to quickly locate "where is the code, what documentation to read first."

## Code Areas to Documentation Entry Points

| Code Area | Focus | Preferred Documentation Entry | Supplementary Entry |
| --- | --- | --- | --- |
| `ColorVision/` | Main program entry, main window, application startup | [Getting Started Guide](../../00-getting-started/README.md) | [User Operation Workflow Matrix](../../01-user-guide/operation-workflow-matrix.md), [Main Window Navigation](../../01-user-guide/interface/main-window.md), [UI Component User Handbook](../../01-user-guide/interface/ui-component-handbook.md) |
| `UI/` | WPF UI framework, themes, editors, DLL publishing | [UI Components Overview](../../04-api-reference/ui-components/README.md) | [UI Component User Handbook](../../01-user-guide/interface/ui-component-handbook.md), [UI Runtime Component Handoff](../../04-api-reference/ui-components/ui-runtime-handoff.md), [UI DLL Component Handbook](../../04-api-reference/ui-components/component-handbook.md), [UI Control Catalog](../../04-api-reference/ui-components/control-catalog.md), [UI DLL Release Playbook](../../04-api-reference/ui-components/ui-dll-release-playbook.md), [UI DLL Release Matrix](../../04-api-reference/ui-components/release-matrix.md) |
| `UI/ColorVision.SocketProtocol/` | TCP service, JSON/Text dispatch, message history, management window | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [Socket Communication Module Optimization Roadmap](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` | Device services, service coordination | [Engine Device Service Chain](../../04-api-reference/engine-components/device-service-chain.md) | [Engine Business Flow Matrix](../../04-api-reference/engine-components/business-flow-matrix.md), [Engine Business Scenario Playbook](../../04-api-reference/engine-components/business-scenario-playbook.md), [Engine Runtime Object Map](../../04-api-reference/engine-components/runtime-object-map.md) |
| `Engine/ColorVision.Engine/Templates/` | Template system, parameterized algorithms, result processing | [Engine Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md) | [Current Algorithm Template Coverage](../../04-api-reference/algorithms/current-algorithm-template-coverage.md), [BuzProduct Business Template](../../04-api-reference/algorithms/templates/buz-product-template.md), [Validate Rule Templates](../../04-api-reference/algorithms/templates/validate-rules.md), [Compliance Result Handoff](../../04-api-reference/algorithms/templates/compliance-results.md), [DataLoad Template](../../04-api-reference/algorithms/templates/data-load-template.md), [Matching Template](../../04-api-reference/algorithms/templates/matching-template.md), [SysDictionary Template](../../04-api-reference/algorithms/templates/sys-dictionary-template.md), [FocusPoints Template](../../04-api-reference/algorithms/templates/focus-points-template.md), [ImageCropping Template](../../04-api-reference/algorithms/templates/image-cropping-template.md), [Template Menu Entries](../../04-api-reference/algorithms/templates/template-menu-entries.md), [Engine Result Display And Project Handoff](../../04-api-reference/engine-components/result-handoff-chain.md) |
| `Engine/FlowEngineLib/` | Flow nodes, execution model, visual flow | [Engine Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md) | [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md), [FlowNode Development](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV integration, low-level vision processing | [Engine Components Overview](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Engine/ColorVision.ShellExtension/` | Explorer thumbnail extension for `.cvraw` / `.cvcie` files | [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) | [Engine Components Overview](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | Runtime plugins and extension capabilities | [Existing Plugin Capabilities](../../04-api-reference/plugins/README.md) | [Current Plugin Documentation Coverage](../../04-api-reference/plugins/current-plugin-coverage.md), [Plugin Runtime And Handoff Playbook](../../04-api-reference/plugins/plugin-handoff-playbook.md), [Existing Plugin Field Acceptance And Handoff Checklist](../../04-api-reference/plugins/plugin-field-acceptance.md), [Plugin Capability & Handoff Matrix](../../04-api-reference/plugins/plugin-capability-matrix.md), [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | Client projects, custom business assembly | [Project Package Overview](../../04-api-reference/projects/README.md) | [Current Project Documentation Coverage](../../04-api-reference/projects/current-project-coverage.md), [Project Package Runtime And Handoff Playbook](../../04-api-reference/projects/project-package-playbook.md), [Project Capability & Handoff Matrix](../../04-api-reference/projects/project-capability-matrix.md), [Project Package Handoff Manual](../../04-api-reference/projects/project-handoff.md) |
| `ColorVisionSetup/` | Installer and update flow | [Deployment Overview](../../02-developer-guide/deployment/overview.md) | [Auto Update System](../../02-developer-guide/deployment/auto-update.md) |
| `Web/Backend/` | Plugin marketplace backend | [Plugin Marketplace Backend](../../02-developer-guide/backend/README.md) | [Development Guide](../../02-developer-guide/README.md) |
| `Scripts/` | Build, packaging, and release scripts | [Build and Release Scripts](../../02-developer-guide/scripts/README.md) | [Deployment Overview](../../02-developer-guide/deployment/overview.md) |
| `Test/` | xUnit, native helper, backend, and script validation | [Testing and Validation Handoff](../../02-developer-guide/testing.md) | [Development Guide](../../02-developer-guide/README.md) |
| `docs/` | Current documentation site source | [Appendix & Resources](../README.md) | Current documentation |

## Search by Task

### Want to Add a Device Service

1. First read [Device Services Overview](../../01-user-guide/devices/overview.md)
2. Then read [Engine Device Service Chain](../../04-api-reference/engine-components/device-service-chain.md)
3. Continue to [Engine Business Scenario Playbook](../../04-api-reference/engine-components/business-scenario-playbook.md)
4. Continue to [Engine Development Guide](../../02-developer-guide/engine-development/README.md)
5. Finally go to [Engine Components Overview](../../04-api-reference/engine-components/README.md) to find specific module pages

### Want to Handle an Engine Business Change

1. [Engine Business Flow Matrix](../../04-api-reference/engine-components/business-flow-matrix.md)
2. [Engine Business Scenario Playbook](../../04-api-reference/engine-components/business-scenario-playbook.md)
3. [Engine Business Handoff](../../04-api-reference/engine-components/business-handoff.md)
4. [Engine Runtime Object Map](../../04-api-reference/engine-components/runtime-object-map.md)
5. Then continue to [Device Service Chain](../../04-api-reference/engine-components/device-service-chain.md), [Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md), [Result Display And Project Handoff](../../04-api-reference/engine-components/result-handoff-chain.md), project-package, or Socket pages based on the scenario

### Want to Develop a Plugin

1. [Extensibility Overview](../../02-developer-guide/core-concepts/extensibility.md)
2. [Plugin Runtime And Handoff Playbook](../../04-api-reference/plugins/plugin-handoff-playbook.md)
3. [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md)
4. [Plugin Capability & Handoff Matrix](../../04-api-reference/plugins/plugin-capability-matrix.md)
5. [Current Plugin Documentation Coverage](../../04-api-reference/plugins/current-plugin-coverage.md)
6. [Existing Plugin Field Acceptance And Handoff Checklist](../../04-api-reference/plugins/plugin-field-acceptance.md)
7. [Plugin Development Getting Started](../../02-developer-guide/plugin-development/getting-started.md)

### Want to Understand Templates or Flows

1. [Algorithms Overview](../../04-api-reference/algorithms/README.md)
2. [Current Algorithm Template Coverage](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)
3. [Engine Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md)
4. [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md)
5. [Templates Architecture Design](../../03-architecture/components/templates/design.md)
6. [Templates API Reference](../../04-api-reference/algorithms/templates/api-reference.md)
7. For concrete handoff pages, continue to [FindLightArea Template](../../04-api-reference/algorithms/templates/find-light-area.md), [JND Template](../../04-api-reference/algorithms/templates/jnd-template.md), [LED Detection Templates](../../04-api-reference/algorithms/templates/led-detection.md), [BuzProduct Business Template](../../04-api-reference/algorithms/templates/buz-product-template.md), [Validate Rule Templates](../../04-api-reference/algorithms/templates/validate-rules.md), [Compliance Result Handoff](../../04-api-reference/algorithms/templates/compliance-results.md), [DataLoad Template](../../04-api-reference/algorithms/templates/data-load-template.md), [Matching Template](../../04-api-reference/algorithms/templates/matching-template.md), [SysDictionary Template](../../04-api-reference/algorithms/templates/sys-dictionary-template.md), [FocusPoints Template](../../04-api-reference/algorithms/templates/focus-points-template.md), [ImageCropping Template](../../04-api-reference/algorithms/templates/image-cropping-template.md), or [Template Menu Entries](../../04-api-reference/algorithms/templates/template-menu-entries.md)

### Want to Modify UI or Property Editing

1. [User Guide](../../01-user-guide/README.md)
2. [UI Component User Handbook](../../01-user-guide/interface/ui-component-handbook.md)
3. [UI Components Overview](../../04-api-reference/ui-components/README.md)
4. [UI Runtime Component Handoff](../../04-api-reference/ui-components/ui-runtime-handoff.md)
5. [UI Control Catalog](../../04-api-reference/ui-components/control-catalog.md)
6. [Property Editor](../../01-user-guide/interface/property-editor.md)

### Want to Publish UI DLLs

1. [UI DLL Release Playbook](../../04-api-reference/ui-components/ui-dll-release-playbook.md)
2. [UI DLL Publishing](../../04-api-reference/ui-components/publishing.md)
3. [UI DLL Release Matrix](../../04-api-reference/ui-components/release-matrix.md)
4. [Build and Release Scripts](../../02-developer-guide/scripts/README.md)

### Want to Find a User Operation Path

1. [User Operation Workflow Matrix](../../01-user-guide/operation-workflow-matrix.md)
2. [Main Window Navigation](../../01-user-guide/interface/main-window.md)
3. [UI Component User Handbook](../../01-user-guide/interface/ui-component-handbook.md)
4. Continue to device, workflow, data, project, or plugin pages based on the operation goal

### Want to Maintain a Customer Project

1. [Project Guide](../../00-projects/README.md)
2. [Project Capability & Handoff Matrix](../../04-api-reference/projects/project-capability-matrix.md)
3. [Current Project Documentation Coverage](../../04-api-reference/projects/current-project-coverage.md)
4. [Project Package Runtime And Handoff Playbook](../../04-api-reference/projects/project-package-playbook.md)
5. [Project Package Handoff Manual](../../04-api-reference/projects/project-handoff.md)
6. Open the project source folder under `Projects/<Name>/`

### Want to Check Build, Release, and Updates

1. [Deployment Overview](../../02-developer-guide/deployment/overview.md)
2. [Auto Update System](../../02-developer-guide/deployment/auto-update.md)
3. [Build and Release Scripts](../../02-developer-guide/scripts/README.md)

### Want to Choose Test and Acceptance Commands

1. [Testing and Validation Handoff](../../02-developer-guide/testing.md)
2. Match the changed module to `Test/ColorVision.UI.Tests/`, `Test/opencv_helper_test/`, backend tests, script tests, or `npm run docs:build`

## Usage Principles

- Enter from chapter home pages first, then jump to specific topic pages.
- Historical drafts, orphaned documents, and old path pages are no longer used as primary entry points.
- If a perfectly matching topic page cannot be found, fall back to the overview page first rather than continuing to rely on old directory naming.
