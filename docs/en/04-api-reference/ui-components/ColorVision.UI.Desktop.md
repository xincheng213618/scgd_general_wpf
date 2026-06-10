# ColorVision.UI.Desktop

This page only describes the desktop-side windows and supporting features currently implemented in UI/ColorVision.UI.Desktop, no longer continuing the old documentation's writing style of "entire system main program entry point."

## Module Positioning

`ColorVision.UI.Desktop` is currently closer to a collection of desktop-side auxiliary shell features, primarily providing:

- Settings window
- Configuration wizard
- Menu item management window
- Configuration management window
- Third-party application integration
- Auxiliary windows like DLL version viewing

It is not the entire repository's main application entry point. The current real main program project is in `ColorVision/`, and the `App.xaml.cs` and `MainWindow.xaml.cs` here are both very lightweight.

## Most Critical Directories

From the project directory, the most worthwhile to read first are:

- `Settings/`: Unified settings window
- `Wizards/`: Wizard window, step discovery, window configuration
- `MenuItemManager/`: Menu item management and persistence
- `ThirdPartyApps/`: System tools and third-party application entry points
- `Marketplace/`: Auxiliary windows like DLL version viewing
- `ConfigManagerWindow.xaml(.cs)`: Configuration management window
- `Feedback/`, `Download/`, `TimedButtons/`, `WebViewService.cs`: Other desktop auxiliary capabilities

## Key Entry Point Types

### App and MainWindow

The current `App.xaml.cs` is just a very lightweight `Application` partial, and `MainWindow.xaml.cs` only retains basic construction logic.

This indicates:

- This project does have `App` and `MainWindow`
- But they are not the central files carrying the complete startup flow and system initialization logic described in old documentation

When reading this project, what is truly more worthwhile to look at first are the individual feature windows and managers, rather than focusing on the empty shell entry points.

### SettingWindow

`Settings/SettingWindow.xaml.cs` is the main desktop entry point for the current settings system. It is responsible for:

- Reading `ConfigSettingManager.GetInstance().GetAllSettings()`
- Creating Tabs by group
- Deciding to generate tab pages, full-class property pages, or single property controls based on `ConfigSettingType`
- Lazily loading `ViewType` to avoid instantiating all views at once during window initialization

Therefore, the old documentation on this page was correct about the direction of "unified settings window," but implementation details should land on `ConfigSettingManager` + lazy loading.

### WizardManager / WizardWindow / WizardWindowConfig

The current wizard chain consists of these types:

- `WizardManager`: Reflectively scans `IWizardStep`
- `WizardWindow`: Multi-step window and completion logic
- `WizardWindowConfig`: Window configuration and completion status

`WizardManager` iterates assemblies and instantiates `IWizardStep`, then sorts by `Order`; `WizardWindow` drives progress bar, forward/back step switching, and completion validation.

This is the clearest "desktop auxiliary flow chain" in the current project.

### MenuItemManagerConfig and MenuItemManagerWindow

`MenuItemManagerConfig` currently handles persistence of menu item settings, while `MenuItemManagerWindow` provides a visual management interface. They belong to the UI shell configuration tools, not the global menu runtime itself.

### ConfigManagerWindow

`ConfigManagerWindow` is another desktop-side management window used to manage configuration items from a more centralized perspective. It does not completely overlap with `SettingWindow` and belongs to the desktop tool layer rather than the foundational interface layer.

### ViewDllVersionsWindow

`Marketplace/ViewDllVersionsWindow.xaml.cs` currently iterates loaded assemblies and collects:

- Name
- Assembly version
- File version
- Product version
- Company information
- Path

It is more like a runtime diagnostic and troubleshooting window rather than the plugin update core flow itself.

### SystemAppProvider and WebViewService

- `ThirdPartyApps/SystemAppProvider.cs` handles system tools and third-party application entry points.
- `WebViewService.cs` indicates that this project also carries some desktop WebView-related capabilities.

## Current Runtime Main Chain

This project currently has no single main chain, but several desktop auxiliary chains coexist. What is more worth paying attention to is:

1. Settings chain: `SettingWindow` -> `ConfigSettingManager` -> Configuration page/property page lazy loading.
2. Wizard chain: `WizardManager` -> `IWizardStep` discovery -> `WizardWindow` switching and completion.
3. Management chain: `MenuItemManagerWindow` / `ConfigManagerWindow` / `ViewDllVersionsWindow` providing desktop management windows from different perspectives.

## Using It as a DLL/Desktop Toolkit

### When to Reference It

- A unified settings window or configuration import/export experience is needed.
- First-run configuration, installation, or multi-step wizard flows are needed.
- Plugin marketplace, DLL version viewing, download management, or third-party application entries are needed.
- Feedback windows, WebView Markdown rendering, or timed-button operation statistics are needed.

### Adding a Settings Page

1. Expose settings metadata through a business configuration class or Provider.
2. Confirm `ConfigSettingManager` can collect the setting item.
3. If using a custom View, confirm `SettingWindow` lazy loading can instantiate it.
4. Open the settings window and verify grouping, saving, and post-restart recovery.

### Adding a Wizard Step

1. Implement `IWizardStep`.
2. Set `Order` and display metadata.
3. Ensure the step assembly is loaded.
4. Open `WizardWindow` and verify ordering, forward/back navigation, completion conditions, and completion state.

### Release Notes

`ColorVision.UI.Desktop` currently packages `Assets/css/github-markdown.css` and `Assets/Tool/aria2c.exe`. Marketplace, download, and Markdown rendering issues must check whether these resources are copied to the output directory.

### DLL/Toolkit Release Acceptance

| Check | What to Inspect | Passing Standard |
| --- | --- | --- |
| Target framework output | `net10.0-windows7.0`, `OutputType=WinExe` | Desktop toolkit assembly, `.nupkg`, and `.snupkg` are produced |
| Package README | `PackageReadmeFile`, package root | `README.md` is included at the package root |
| Project dependencies | `ColorVision.UI`, `ColorVision.Database` | Settings, database configuration, and shell dependencies resolve |
| WebView2/Markdown | `Microsoft.Web.WebView2`, `Markdig.Signed`, `Assets/css/github-markdown.css` | Marketplace or Markdown preview initializes and uses CSS |
| Download tool | `Assets/Tool/aria2c.exe`, `Aria2cDownloadManager` | Output contains `aria2c.exe`, and download window can start or reuse the RPC daemon |
| Settings window | `SettingWindow`, `ConfigSettingManager` | Groups, search, lazy-loaded Views, saving, and restart recovery work |
| Wizard window | `WizardManager`, `WizardWindow` | `IWizardStep` discovery, ordering, navigation, and completion state work |
| Diagnostics window | `ViewDllVersionsWindow` | Loaded assemblies, file versions, product versions, and paths are listed |

### Field First Checks

| Symptom | First Check |
| --- | --- |
| Settings page is empty or missing items | Check whether `ConfigSettingManager` collected Providers and whether Provider assemblies loaded |
| Wizard steps do not appear | Check whether steps implement `IWizardStep`, assembly loading, and abnormal `Order` values |
| Marketplace README/CHANGELOG is blank | Check WebView2 initialization first, then whether Markdown CSS copied to `Assets/css/` |
| Download fails or hangs | Check `Assets/Tool/aria2c.exe`, RPC port, and old aria2c processes |
| DLL version window misses entries | Check whether the target assemblies are actually loaded into the current AppDomain |
| Third-party app entry fails | Check paths generated by `SystemAppProvider`, permissions, and whether the system tool exists |

## What Boundaries the Current Implementation Has

### It Is Not the Entire System Main Entry Point

This is where this page is most easily miswritten. The `App` and `MainWindow` in the current project are both very lightweight and cannot continue to be described as the sole startup center of the entire product.

### Not All Features Revolve Around MainWindow

This project is more like a collection of windows and management tools. Much value comes from independent windows rather than a massive main window orchestration layer.

### The SystemInitializer Mentioned in Old Documentation Does Not Exist in This Project

The current `UI/ColorVision.UI.Desktop` directory has no actual `SystemInitializer` implementation. Old documentation listing it as an existing component would directly mislead readers into searching for a non-existent control point.

## How to Better Read This Module Currently

### To View Settings and Configuration Windows

Read first:

- `Settings/SettingWindow.xaml.cs`
- `ConfigManagerWindow.xaml.cs`

### To View Wizard and First-Time Configuration Flow

Read first:

- `Wizards/WizardWindow.xaml.cs`
- `Wizards/WizardWindowConfig.cs`

### To View Menu Management and Desktop Auxiliary Windows

Read first:

- `MenuItemManager/MenuItemManagerConfig.cs`
- `MenuItemManager/MenuItemManagerWindow.xaml.cs`
- `Marketplace/ViewDllVersionsWindow.xaml.cs`
- `ThirdPartyApps/SystemAppProvider.cs`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Writing this project as the entire system main program entry point
- Descriptions of non-existent components, such as `SystemInitializer`
- Extensive version numbers and pseudo-API lists
- Expanding the lightweight `App` / `MainWindow` into a complete startup flow center

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
