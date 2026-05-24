# WindowsServicePlugin

This page only describes the WindowsServicePlugin implementation that actually exists in the current repository, no longer maintaining the old "operations platform complete manual + comprehensive API directory" style draft.

## What This Plugin Is Now

Based on current source code status, WindowsServicePlugin is not simply a collection of "service log shortcuts," but a plugin package built around local Windows service operations. The clearest capability lines currently are:

- Service manager entry in the Help menu.
- Service installation and update window.
- Service log and local log directory shortcut entries.
- Bridging with CVWinSMS configuration files and update packages.
- Configuration reading and CFG overwriting in Wizard steps.

Therefore, it is more specific than the generic "service toolbox" in old documentation — the actual center is the two control chains of `ServiceManagerViewModel` and `ServiceInstallViewModel`.

## Most Critical Files

- `Plugins/WindowsServicePlugin/manifest.json`
- `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerWindow.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallWindow.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
- `Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
- `Plugins/WindowsServicePlugin/SetMysqlConfig.cs`
- `Plugins/WindowsServicePlugin/SetServiceConfig.cs`
- `Plugins/WindowsServicePlugin/Menus/ServiceLog.cs`

If you just want to understand how the plugin integrates into the host, how to open the service manager, and how to do configuration synchronization and updates, these files already cover the main body.

## Integration Chains into the Host

### Service Manager Entry in Help Menu

`MenuServiceManager` is currently placed under the `Help` menu, directly opening `ServiceManagerWindow` upon execution.

Beyond this, `ServiceManagerAppProvider` in the same file also implements `IThirdPartyAppProvider`, exposing "Service Manager" as an internal tool to the host's third-party application entry point.

This means it is not just a single menu command, but has at least two UI integration chains.

### Service Log Menu Tree

`ServiceLog` is currently also a root menu item under the `Help` menu. Around it, the plugin continues to inject multiple groups of log shortcut entries:

- HTTP log page
- Local log directories resolved from `CVWinSMSConfig.BaseLocation`

For example, types like `ExportRCServiceLog` and `Exportx64ServiceLog` directly open local URLs; while directory versions with suffixes concatenate the `log` folder under the service directory.

### Wizard and Initialization Entry Point

`InstallTool` currently implements simultaneously:

- `MenuItemBase`
- `IWizardStep`
- `IMainWindowInitialized`

It can serve as a menu entry to open or locate CVWinSMS, check for updates after main window initialization, and enter the wizard's aggregation chain.

Therefore, this plugin is currently not just "only a service management window" — CVWinSMS-related guidance and update logic are also part of the host integration surface.

### Manifest Information

According to the current `manifest.json`, the loading information publicly exposed by the plugin is:

- `Id = "WindowsServicePlugin"`
- `name = "ColorVision Service Plugin"`
- `version = "1.0"`
- `dllpath = "WindowsServicePlugin.dll"`
- `requires = "1.3.12.34"`

This is closer to the current real loading model than the extra dependency matrices pieced together in old documentation.

## How the Service Manager Currently Works

`ServiceManagerWindow` itself is very thin — upon window initialization, it directly sets `DataContext` to `ServiceManagerViewModel.Instance` and auto-scrolls the log area when log text changes.

The real runtime center is in `ServiceManagerViewModel`. Based on current implementation, it is at least responsible for:

- Maintaining the default service list.
- Maintaining MySQL and MQTT managers.
- Maintaining current version, available version, busy status, progress, and log text.
- Exposing administrator mode status and "Restart as Administrator" command.
- Exposing one-click start/stop, refresh, open directory, and open config file commands.

### Currently Managed Default Services

`ServiceManagerConfig.GetDefaultServiceEntries()` currently explicitly lists:

- `RegistrationCenterService`
- `CVMainService_x64`
- `CVMainService_dev`
- `CVArchService`

So this page should be written around these real service items, rather than continuing to generalize as an "arbitrary service orchestration framework."

### Path and Version Detection

`ServiceManagerConfig` currently preferentially attempts:

1. Read the installation path from the registry's `RegistrationCenterService`.
2. If that fails, attempt to read `BaseLocation` and `MysqlPort` from CVWinSMS's `App.config`.

`RefreshAll()` also refreshes each service status and updates the current version display based on `RegistrationCenterService`'s version text.

## How the Installation and Update Chain Currently Unfolds

`ServiceInstallWindow` itself is similarly very thin, with core logic in `ServiceInstallViewModel`. What this chain currently truly manages is:

- Service installation package selection
- MySQL ZIP selection
- MQTT installer selection
- Download directory selection
- Online update checking
- Backup and restore
- One-click install all components

Based on current implementation, this window is not concerned with a single "download latest version," but with complete installation orchestration state, including progress, logs, auto-start, database update, and backup toggles.

## Current Relationship with CVWinSMS

`CVWinSMSConfig` is responsible for maintaining `CVWinSMSPath`, update address, and auto-update toggle, and provides `BaseLocation` parsed from the external `App.config`.

`InstallTool` is responsible for:

- Detecting existing CVWinSMS executable.
- Downloading update packages when needed.
- Extracting and replacing old directories.
- Restarting CVWinSMS with administrator privileges.

This shows that WindowsServicePlugin is currently not a standalone closed set of service operations UI, but explicitly carries bridging and migration logic with the external CVWinSMS tool.

## How Wizard Steps Currently Land

### Reading Service Configuration

`SetMysqlConfig` reads `config/App.config` under the CVWinSMS directory and writes the MySQL configuration from it back into the database configuration object used by the current host.

### Overwriting Service CFG

`SetServiceConfigStep` reads the same `App.config` and then uses the current host's:

- MySQL settings
- MQTT settings
- RC settings

to update the following in the service directory:

- `cfg/MySql.config`
- `cfg/MQTT.config`
- `cfg/WinService.config`

After writing back, it also attempts to restart `RegistrationCenterService`. This is a chain that truly modifies server-side configuration and should not continue to be written as a "regular wizard button."

## Most Common Mistakes to Avoid

### It Is Not Just a Log Menu Plugin

Although there is indeed a whole set of log entries, the plugin body remains the service management and installation update control chains. Writing only about log menus would shrink the main implementation surface too much.

### The `Application` Shell Is Not the Focus of Host Extensions

The repository has `App.xaml.cs`, but it currently serves more as a standalone launch or debug shell. For main program plugin documentation, the focus should be on manifest, menus, providers, view models, and wizard steps, rather than mistakenly treating this `Application` type as the everyday plugin entry point.

### Configuration Synchronization Really Modifies Server-Side CFG

`SetServiceConfigStep` is not a read-only checker. It writes the current host configuration back to configuration files under multiple service directories and attempts to restart the registration center service.

### The Service Manager Is Currently a Singleton Center

`ServiceManagerViewModel.Instance` is the shared state center for the current window and commands. Continuing to write it as a "reconstruct a set of context each time the window opens" model would conflict with the current implementation.

## Recommended Reading Order

1. `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
2. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
3. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
4. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
5. `Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
6. `Plugins/WindowsServicePlugin/SetMysqlConfig.cs`
7. `Plugins/WindowsServicePlugin/SetServiceConfig.cs`
8. `Plugins/WindowsServicePlugin/Menus/ServiceLog.cs`
9. `Plugins/WindowsServicePlugin/manifest.json`

This allows seeing the host entry point first, then the state center, configuration bridging, and installation chain.

## Continue Reading

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/eventvwr.md](./eventvwr.md)