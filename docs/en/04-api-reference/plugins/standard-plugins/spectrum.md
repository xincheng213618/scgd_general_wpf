# Spectrum Plugin

This page only describes the Spectrum plugin implementation that actually exists in the current repository, no longer maintaining the old "version table + feature promotion + idealized API manual" style draft.

## What This Plugin Is Now

Based on current source code status, Spectrum is not a scattered device driver example, but a plugin workbench centered around an independent spectral measurement window. It currently contains at least four clear runtime chains:

- Window entry point in the Tool menu.
- Custom menus and status bar for the `Spectrum` target window.
- Connection, calibration, and measurement control centered around `SpectrometerManager`.
- Result display, SQLite persistence, and measurement profile recording centered around `ViewResultManager`.

Therefore, it is more specific than the generic "spectrometer test tool" description in old documentation — it is actually a complete but still single-window-centered measurement workbench.

## Most Critical Files

- `Plugins/Spectrum/manifest.json`
- `Plugins/Spectrum/MainWindow.xaml(.cs)`
- `Plugins/Spectrum/MainWindow.Connection.cs`
- `Plugins/Spectrum/MainWindow.Measurement.cs`
- `Plugins/Spectrum/SpectrometerManager.cs`
- `Plugins/Spectrum/Data/ViewResultManager.cs`
- `Plugins/Spectrum/SpectrumStatusBarProvider.cs`
- `Plugins/Spectrum/Calibration/CalibrationGroupWindow.xaml.cs`
- `Plugins/Spectrum/License/LicenseDatabase.cs`

If you just want to understand how the plugin integrates into the host, how to connect devices, and how to save results, these files already cover the main body.

## Integration Chains into the Host

### Window Entry Point

`MenuSpectrumWindow` currently inherits `MenuItemBase` and is placed under the `Tool` menu, directly opening `MainWindow` upon execution.

This shows that Spectrum's most core host entry point is not a thick plugin entry class, but the menu item and the subsequently opened work window.

### Window-Level Menus and Status Bar

During `MainWindow` initialization, the following are called:

- `MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu)`
- `StatusBarManager.GetInstance().Init(StatusBarGrid, "Spectrum")`

In other words, this plugin does not just hang an entry point on the main program menu. After the window opens, there is also a set of local menu and status bar extension surfaces targeting `TargetName = "Spectrum"`.

### Status Bar Provider

`SpectrumStatusBarProvider` currently connects the following information to the `Spectrum` window status bar:

- Connection status
- Hardware model
- SN serial number
- Current calibration group
- Current measurement mode
- Shutter connection status
- CFW filter wheel connection status

Among these, the SN text currently supports click-to-copy, so it is not just a read-only decoration item.

### Manifest Information

According to the current `manifest.json`, the loading information publicly exposed by the plugin to the host is:

- `Id = "Spectrum"`
- `name = "Spectrum"`
- `version = "1.0"`
- `dllpath = "Spectrum.dll"`
- `requires = "1.3.15.8"`

This is closer to the current real loading model than the custom long version and dependency tables in old documentation.

## What Is the Current Runtime Core

`SpectrometerManager` is the most important singleton state center of the current plugin. It obtains and holds via `ConfigService`:

- `SpectrumConfig`
- `ShutterController`
- `FilterWheelController`
- `SmuController`
- Current device handle
- Current connection status, hardware model, serial number
- Current calibration group configuration and active group
- Current measurement mode text

Therefore, `MainWindow` is more about organizing UI and invocation chains, while the truly cross-file shared measurement state is primarily consolidated in `SpectrometerManager`.

## How Connection and Calibration Currently Work

`MainWindow.Connection.cs` demonstrates the real sequence of the current connection chain:

1. Before connecting, first call `LicenseDatabase.Instance.SyncToLocal()` to synchronize licenses.
2. Create a handle via `Spectrometer.CM_CreateEmission(...)`.
3. Decide whether to initialize via USB or COM port based on configuration.
4. Read device serial number after successful connection.
5. Load calibration group configuration by serial number.
6. Load current wavelength calibration file and amplitude calibration file.
7. Apply SP100 parameters.

If the connection fails, the current implementation also attempts to read the device list; when a single device is detected but connection fails, it directs the issue to license management rather than just showing a generic error dialog.

### Calibration Groups Are Not Simple File Selection Dialogs

`CalibrationGroupWindow` currently manages group configurations by spectrometer SN. Changes during editing are temporarily stored in memory and only truly written back when save is clicked; closing the window directly discards unsaved changes.

Compared to the old documentation's description of "select a calibration file and continue measuring," this is already a much clearer per-device-group configuration model.

## How the Measurement Chain Currently Unfolds

`MainWindow.Measurement.cs` currently breaks a single measurement into several clear stages:

- Auto-zero pre-check
- Auto-integration
- Adaptive zero calibration
- Data acquisition
- Result rendering
- Result persistence

In terms of specific behavior, the current implementation is already more than just "call device SDK once and draw a chart":

- Auto-zero depends on `ShutterController`.
- Sync frequency mode uses `CM_Emission_GetDataSyncfreq(...)`.
- Standard mode retries once on timeout.
- EQE mode integrates `SmuController` and writes voltage/current results back to window configuration and result objects.

Additionally, the measurement process records timing, input snapshots, and success status for each step.

## How Results and Persistence Currently Land

`ViewResultManager` is currently not just an in-memory list manager, but the plugin's data landing point. Based on implementation, it:

- Maintains a SQLite database at `AppData\Spectromer\Config\Spectrum.db`.
- Saves `SprectrumModel` result records.
- Maintains `SpectrumMeasurementProfile` measurement profiles.
- Saves measurement step detail JSON.
- Updates EQE fields and total measurement elapsed time when needed.

Therefore, Spectrum is currently not a "measure and discard" temporary tool — it already includes basic data tracking and review capabilities.

### Export and List Operations

The current main window also has built-in:

- Relative spectrum / absolute spectrum switching
- CIE chart linked display
- Visible column copy
- Standard spectrum CSV export
- EQE mode CSV export
- Result deletion and database clearing

These behaviors are distributed across `MainWindow.Chart.cs`, `MainWindow.ListView.cs`, and `MainWindow.Export.cs`.

## Additional Subsystems Currently Present

### Layout Persistence

`MainWindow` currently manages AvalonDock layouts via `DockLayoutManager` and automatically saves the layout on window close. This means it is not a rigid single-panel window.

### License Synchronization

`LicenseDatabase` currently uses SQLite to track metadata of imported license files and synchronizes the global license directory with the local `license` directory before connecting to the spectrometer.

### Standalone Launch Shell Exists, But Is Not the Focus of Host Extensions

The repository does have `App.xaml.cs`, which initializes themes, logging, sockets, and the main window when launched standalone. However, in the current main program plugin loading model, documentation should focus more on manifest, menu entry points, providers, and the window body, rather than mistakenly writing this `Application` class as the everyday host extension entry point.

## Most Common Mistakes to Avoid

### It Is Not Just "Connect Device + Read One Frame of Data"

The current Spectrum implementation has already connected license synchronization, calibration groups, window layout, status bar, SQLite result storage, and measurement profiles. Continuing to write it as a lightweight test tool would significantly underestimate the current complexity.

### Result Persistence Is Not Just One Table

Beyond spectral result records, it also separately stores `SpectrumMeasurementProfile` and step detail JSON. If old documentation only writes about CSV export, it would miss the real tracking chain.

### Calibration Is Organized by SN

The current calibration configuration is not a simple global single-file path, but bound to serial number and active group. This boundary is important for understanding on-site device switching.

### The Status Bar Is a Window-Level Extension, Not a Global Main Program Persistent Item

`SpectrumStatusBarProvider`'s target name is `Spectrum` — it describes the plugin window's internal status bar, not a global status bar visible on any main program page.

## Recommended Reading Order

1. `Plugins/Spectrum/MainWindow.xaml.cs`
2. `Plugins/Spectrum/SpectrometerManager.cs`
3. `Plugins/Spectrum/MainWindow.Connection.cs`
4. `Plugins/Spectrum/MainWindow.Measurement.cs`
5. `Plugins/Spectrum/Data/ViewResultManager.cs`
6. `Plugins/Spectrum/SpectrumStatusBarProvider.cs`
7. `Plugins/Spectrum/Calibration/CalibrationGroupWindow.xaml.cs`
8. `Plugins/Spectrum/manifest.json`

This allows seeing the host entry point first, then the state center, device chain, and result landing points.

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Plugin loading | Check `manifest.json`, `dllpath`, and the Tool menu | The Tool menu shows Spectrum, and `MainWindow` opens |
| Window extension | Open the Spectrum window | The `TargetName = "Spectrum"` menu and status bar load; the status bar shows connection, model, SN, calibration group, and mode |
| Delivery resources | Inspect the build output or `.cvxp` package | `Spectrum.dll`, `manifest.json`, `README.md`, `CHANGELOG.md`, calibration files, and required native DLLs exist |
| License synchronization | Open license management or connect before measurement | `LicenseDatabase` synchronizes global and local licenses; license failures guide the user to license management |
| Device connection | Connect a known spectrometer with field USB/COM settings | A handle is created, model and SN are read, and the calibration group for that SN is loaded |
| Calibration groups | Edit a group, save, close, and reopen | Saved groups restore by SN; closing without saving does not pollute configuration |
| Standard measurement | Run standard measurement mode | The chart refreshes, the result list adds a row, and step timings/input snapshots are recorded |
| EQE measurement | Configure SMU and run EQE mode | Voltage, current, and EQE fields are written to result objects and can be exported |
| Database landing | Inspect `AppData\Spectromer\Config\Spectrum.db` | `SprectrumModel`, `SpectrumMeasurementProfile`, and measurement step JSON are present |
| Export and list actions | Run CSV/EQE CSV export, copy visible columns, delete, and clear | File columns are correct, and list/database operations match window prompts |
| Socket commands | Call measurement handlers when Socket is enabled | Connect, status, measure, dark, and auto-integration commands return readable states |

## First Checks

| Symptom | Check first |
| --- | --- |
| Tool menu has no Spectrum | Plugin folder, `manifest.json` `Id/dllpath/requires`, and whether `Spectrum.dll` was copied to the host plugin folder |
| Window status bar is empty | Whether `SpectrumStatusBarProvider` is registered, and whether `LoadMenuForWindow("Spectrum", ...)` and `StatusBarManager.Init(..., "Spectrum")` ran |
| Connection fails | License sync, USB/COM settings, device list, native SDK DLLs, device ownership, administrator/driver state |
| SN is empty or calibration fails | Device SN readout, SN-bound calibration group, `WavaLength.dat`, and `Magiude.dat` paths |
| Auto-zero cannot continue | `ShutterController` connection state, dark flow, and auto-zero pre-check result |
| Measurement times out or chart does not refresh | Integration time, sync-frequency mode, SDK return codes, retry result, and chart refresh chain |
| Result list has rows but database is empty | `ViewResultManager` config, SQLite path, write exceptions, and `SpectrumMeasurementProfile` saving |
| EQE fields stay zero | `SmuController` config, window measurement mode, EQE field writeback, and recalculation update |
| Socket command has no response | Whether the Socket handler is enabled and lands on current `MainWindow.ViewResultManager` and `SpectrometerManager` state |
| Export is empty | Current list filter, relative/absolute spectrum toggle, visible columns, and export model |

## Continue Reading

- [Existing Plugin Field Acceptance And Handoff Checklist](../plugin-field-acceptance.md)
- [Plugin Capability & Handoff Matrix](../plugin-capability-matrix.md)
- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)
