# SystemMonitor Plugin

This page only describes the SystemMonitor plugin implementation that actually exists in the current repository, no longer maintaining the old "version info + tuning manual + idealized architecture diagram" style draft.

## What This Plugin Is Now

Based on current source code status, SystemMonitor is a relatively lightweight system monitoring plugin. Its core is not an independent application shell, but a set of integration points built around a singleton monitoring service:

- `SystemMonitors`: Central singleton for monitoring data and commands.
- `SystemMonitorProvider`: Connects the plugin to the settings page and Tool menu.
- `SystemMonitorIStatusBarProvider`: Connects optional monitoring items to the main program status bar.
- `SystemMonitorControl`: WPF control that actually displays monitoring data.

Therefore, it is closer to "system monitoring service + UI integration layer" rather than a heavyweight standalone window application.

## Most Important Files

- `Plugins/SystemMonitor/manifest.json`
- `Plugins/SystemMonitor/SystemMonitors.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml(.cs)`
- `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`

Among these, `SystemMonitors.cs` handles the vast majority of real runtime logic; the other two types primarily handle connecting it to host UI.

## What the Current Feature Surface Actually Includes

From the implementation of `SystemMonitors`, the monitoring surface currently covered by this plugin is clearly broader than the "time + RAM" described in old documentation:

### Performance Counters

The plugin asynchronously initializes Windows performance counters and periodically updates:

- System CPU usage
- Current process CPU usage
- System RAM usage
- Current process private working set

If performance counter initialization fails, the current implementation degrades by not refreshing these values rather than aborting the entire plugin.

### Disk and Network

The plugin currently actively loads and maintains:

- Capacity, used space, free space, and usage ratio for all ready disks
- Non-loopback/tunnel network interface information
- Network interface IPv4 addresses, MAC addresses, link speed, and status

This data does not depend on status bar toggles — the status bar only decides whether to project a portion of it to the bottom of the main window.

### Processes and Runtime Environment

It also currently collects:

- Top 10 high-memory-usage processes
- Current process thread count and handle count
- System boot time, application uptime, system uptime
- CPU name, hostname, .NET runtime, system architecture, username
- Primary display resolution

### GPU and Cache

The plugin also reads `ConfigCuda.Instance`, displaying CUDA device name and video memory information when available; it also provides cache size statistics and a clear command.

## Three Chains Connecting to the Host

### Settings Page

`SystemMonitorProvider` implements `IConfigSettingProvider`, using `SystemMonitors.GetInstance()` as the settings page data source and `SystemMonitorControl` as the display control.

This means the settings page and the standalone popup window actually display the same singleton data, not two separate monitoring instances.

### Tool Menu

The same `SystemMonitorProvider` also implements `IMenuItemProvider`, currently injecting a "Performance Monitor" entry under the `Tool` menu and opening a regular WPF window hosting `SystemMonitorControl`.

### Status Bar

`SystemMonitorIStatusBarProvider` implements `IStatusBarProviderUpdatable`, dynamically deciding whether status bar items exist based on configuration toggles. Items currently projectable to the status bar include:

- Time
- Application uptime
- CPU text
- RAM text
- Disk icon and remaining space

Therefore, it is not the static two-item status bar provider described in old documentation.

## Current Configuration Model

`SystemMonitorSetting` currently includes at least these toggles and parameters:

- `UpdateSpeed`
- `DefaultTimeFormat`
- `IsShowTime`
- `IsShowRAM`
- `IsShowCPU`
- `IsShowUptime`
- `IsShowDisk`

Old documentation only writing about time and RAM can no longer cover the full scope.

## Current Command Surface

The user commands currently exposed by `SystemMonitors` are primarily:

- `ClearCacheCommand`
- `RefreshDrivesCommand`
- `RefreshNetworkCommand`
- `RefreshProcessesCommand`

The real behaviors corresponding to these commands are, respectively: clearing app data and log directories, reloading disk list, reloading network interface list, reloading high-usage process list.

## Most Common Mistakes to Avoid

### It Is Not a Standalone Window Application-Centered Plugin

Although the menu opens a window, the window only hosts `SystemMonitorControl`. The truly continuously running core object is the `SystemMonitors` singleton.

### It Is Not Just a Status Bar Time Plugin

The current status bar is only one of three integration chains. A large amount of data actually serves the full monitoring control, including disk, network, GPU, process list, and cache statistics.

### `IStatusBarProviderUpdatable` Is Critical

The refresh of status bar display items currently depends on `SystemMonitorIStatusBarProvider` listening for configuration changes and then triggering `StatusBarItemsChanged`. Mistakenly writing it as a regular static provider would misrepresent this current dynamic refresh chain.

### Do Not Assume Type Naming and Namespaces

`SystemMonitors` and `SystemMonitorSetting` are currently located in the `ColorVision.UI.Configs` namespace, not the plugin's own `SystemMonitor` namespace. This is part of the current code reality — do not arbitrarily rewrite it as if "plugins are internally self-contained systems."

## Recommended Reading Order

1. `Plugins/SystemMonitor/SystemMonitors.cs`
2. `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
3. `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`
4. `Plugins/SystemMonitor/manifest.json`

This allows grasping the real control surface first, then returning to menus, status bar, and loading information.

## Continue Reading

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)