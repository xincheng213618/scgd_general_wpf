# ColorVision.Common

This page only describes the shared foundational capabilities currently provided by UI/ColorVision.Common, no longer continuing the old documentation's "comprehensive public SDK interface encyclopedia" writing style.

## Module Positioning

ColorVision.Common is currently the shared foundation library for the UI layer, primarily providing:

- MVVM base types
- Command encapsulation
- Common interfaces and metadata models
- Coarse-grained permission control
- Windows native method wrappers
- Common utility classes

It is more like a set of cross-module reusable building blocks rather than an independently running business module.

## Most Critical Directories

From the project directory, the most worthwhile to recognize first are:

- MVVM/: `ViewModelBase`, `RelayCommand`, and other base types
- Interfaces/: Shared interfaces for configuration, menus, status bar, initializers, views, etc.
- Authorizations/: `Authorization`, `AccessControl`, `PermissionMode`
- NativeMethods/: Windows API wrappers
- Utilities/: Common utilities for files, collections, windows, etc.
- Input/: Input-related capabilities
- ThirdPartyApps/: Third-party application integration related definitions

## Key Entry Point Types

### ViewModelBase

`ViewModelBase` is the most fundamental bindable object base class, implementing `INotifyPropertyChanged` and serving as the base for a large number of configuration classes, managers, and view models.

### RelayCommand and Commands

The current command layer primarily has two commonly used entry points:

- `RelayCommand` / `RelayCommand<T>`: General-purpose command encapsulation
- `Commands`: A small number of global `RoutedUICommand`

Old documentation wrote the command system as an entire independent framework, but from current code, what is truly used frequently is still `RelayCommand`.

### Interfaces/

`Interfaces/` handles shared boundary definitions rather than complete business implementations. The currently common groups of interfaces include:

- `IConfig`, `IConfigSettingProvider`
- `IInitializer`, `InitializerBase`
- `IMenuItemProvider`
- `IStatusBarProvider`, `IStatusBarProviderUpdatable`
- View-related types such as `View` and `IViewManager`

Most of these types only define minimal contracts; the actual registration, discovery, and execution logic is typically in upper-layer modules.

### StatusBarMeta

`StatusBarMeta` is not the simplified model with only icons and commands found in old documentation. Currently it already carries:

- Unique identifier and name
- Description text
- Left/right alignment and ordering
- Two types of actions: `Command` or `Popup`
- Binding source object
- Icon resource or direct icon content
- Target window scope and default visibility

So it is already the core metadata of the UI status bar system, not just a lightweight DTO.

### Authorization / AccessControl / PermissionMode

`Authorizations/` provides the current general-purpose coarse-grained permission control:

- `Authorization.Instance.PermissionMode`
- `AccessControl.Check(...)`
- `RequiresPermissionAttribute`

Important boundary to note here:

- The Common layer only provides global coarse-grained permission modes
- Fine-grained local RBAC is in `UI/ColorVision.Solution/Rbac`

Do not write Common's permission system as the sole complete RBAC for the entire project.

## What the Current Implementation Is More Like

ColorVision.Common is currently closer to "shared protocol layer + base utility layer" rather than a stable public framework released for external users. Although many interfaces have generic names, their real role is to provide unified contracts for UI modules within the repository.

For example:

- `IConfig` itself is just a marker interface
- `InitializerBase` only provides default name, order, and dependency structure
- `View` is a shared ViewModel with index, title, and icon, not a complete view framework

## How to Better Read This Module Currently

### To View Shared MVVM and Command Foundations

Read first:

- `MVVM/ViewModelBase.cs`
- `MVVM/RelayCommand.cs`
- `Commands.cs`

### To View Public Contracts for Configuration, Menus, Status Bar

Read first:

- `Interfaces/Config/` or `Interfaces/ConfigSetting/`
- `Interfaces/Menus/`
- `Interfaces/StatusBar/`
- `Interfaces/IInitializer/`

### To View Permission Boundaries

Read first:

- `Authorizations/AccessControl.cs`
- `Authorizations/PermissionMode.cs`

### To View Native Methods and Utility Classes

Read first:

- `NativeMethods/`
- `Utilities/`

## Boundaries of the Current Implementation

### It Is Not a Complete Plugin Platform Document

Although Common defines many extension interfaces, the actual plugin discovery, menu registration, configuration aggregation, and status bar refresh are distributed across upper-layer module implementations. This is only shared contracts and should not be written as a unified runtime center.

### It Is Not a Complete Permission Center

The permission checks in Common are suitable for global mode toggles or coarse-grained constraints, but do not equal the Solution-side local RBAC.

### Many Interfaces Are "Minimal Shapes" Rather Than "Final Abstractions"

Interfaces like `IConfig` and `IInitializer` are very lightweight; when reading further, you should prioritize following the implementers to see the real control chain rather than staying at the interface definitions themselves.

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Extensive version numbers and package release information
- Idealized public SDK checklists
- Expanding every interface into complete framework capabilities
- Mistakenly writing Common's permission system as the global unique RBAC

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)