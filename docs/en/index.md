---
layout: home

hero:
  name: "ColorVision"
  text: "Documentation Center for Vision Inspection Delivery"
  tagline: Organized by project guide, user manual, developer manual, plugin development manual, and existing plugin capabilities. Start with the delivered business project, then move into operation and implementation details.
  image:
    src: /images/ColorVision.png
    alt: ColorVision
  actions:
    - theme: brand
      text: Project Guide
      link: /en/00-projects/README
    - theme: alt
      text: User Manual
      link: /en/01-user-guide/README
    - theme: alt
      text: Plugin Capabilities
      link: /en/04-api-reference/plugins/README

features:
  - title: Project Guide
    details: Customer projects and solution packages, including business positioning, flow organization, protocol entry points, result export, and handoff order.
    link: /en/00-projects/README
  - title: User Manual
    details: Installation, first run, main window, devices, workflow execution, data management, and troubleshooting for operators.
    link: /en/01-user-guide/README
  - title: Developer Manual
    details: Engine extension, UI DLL publishing, deployment, update packages, build scripts, backend services, and delivery checks.
    link: /en/02-developer-guide/README
  - title: Plugin Development Manual
    details: Plugin interfaces, manifest, loading flow, build copy rules, packaging, publishing, and reference implementations.
    link: /en/02-developer-guide/plugin-development/README
  - title: Existing Plugin Capabilities
    details: Conoscope, Spectrum, SystemMonitor, EventVWR, and WindowsServicePlugin capabilities, entry points, and boundaries.
    link: /en/04-api-reference/plugins/README
  - title: Module Reference
    details: UI DLLs, Engine business chains, algorithm templates, extension points, and source-code anchors.
    link: /en/04-api-reference/README
  - title: Resources
    details: Repository structure, module-to-documentation map, license agreement, and stable appendix material.
    link: /en/05-resources/README
---

## Choose by Goal

| What you need to do | Start here | What you should know after reading |
| --- | --- | --- |
| Take over a customer project | [Project Guide](/en/00-projects/README) | Business purpose, workflow structure, protocol entry points, result export, and maintenance boundaries for each project |
| Install, start, or operate the system | [User Manual](/en/01-user-guide/README) | System requirements, main window, devices, image editor, workflow execution, data, and troubleshooting |
| Modify code, build packages, or deliver a version | [Developer Manual](/en/02-developer-guide/README) | Development entry points and delivery checks for Engine, UI, deployment, scripts, and backend services |
| Add or maintain a plugin | [Plugin Development Manual](/en/02-developer-guide/plugin-development/README) | Plugin interfaces, manifest, loading, build copy, packaging, and publishing conventions |
| Check what current plugins can do | [Existing Plugin Capabilities](/en/04-api-reference/plugins/README) | Capabilities, entry points, dependencies, build notes, and maintenance risks for each plugin |
| Look up source modules | [Module Reference](/en/04-api-reference/README) | Responsibilities, entry points, and key classes for UI, Engine, algorithm templates, and extension points |
| Locate directories quickly | [Resources](/en/05-resources/README) | Repository structure, documentation mapping, and stable appendices |

## Five Main Modules

The documentation is organized so handoff starts with the delivered object, then moves into operation and implementation:

| Module | Directories | Problem it answers |
| --- | --- | --- |
| Project Guide | `00-projects/`, `04-api-reference/projects/` | What the customer project is, how it is triggered, how flows run, how results export, and how to hand it over |
| User Manual | `00-getting-started/`, `01-user-guide/` | How operators install, start, connect devices, run flows, view data, and troubleshoot field issues |
| Developer Manual | `02-developer-guide/`, `03-architecture/`, `04-api-reference/engine-components/`, `04-api-reference/ui-components/` | How developers understand Engine business chains, UI DLLs, build, test, deployment, and release |
| Plugin Development Manual | `02-developer-guide/plugin-development/` | How to add, load, debug, package, and publish a general plugin |
| Existing Plugin Capabilities | `04-api-reference/plugins/` | What current real plugins do, where they enter, what they depend on, and how to accept and troubleshoot them |

## Supporting Material

These sections support the five main modules and are not the first reading entry:

- `04-api-reference/`: module reference for UI DLLs, Engine, algorithm templates, extension points, plugins, and detailed project pages.
- `05-resources/`: project structure, module documentation map, license agreement, and stable appendices.
- Locale directories: after Chinese pages are stable, translate the same structure to English, Traditional Chinese, Japanese, and Korean.

## Maintenance Principles

- The user manual should only describe what operators can see and do.
- Module reference pages must point back to current source folders, project files, manifests, or key classes.
- UI module docs focus on DLL/NuGet publishing, dependency relationships, and consumption patterns.
- Engine module docs focus on the business chain across devices, templates, Flow, MQTT, data, and result display.
- Plugin and project-package docs must match real directories in the current repository; missing historical modules must not be presented as current capabilities.
