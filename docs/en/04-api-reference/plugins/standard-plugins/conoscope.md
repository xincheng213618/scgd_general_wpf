# Conoscope Plugin

Conoscope is the current VAM/conoscope analysis plugin in this repository. Its source is under `Plugins/Conoscope/`. It is used for conoscope image viewing, reference coordinate analysis, focus point sampling, combined color gamut calculation, and black/white contrast calculation.

## manifest

| Field | Current value |
| --- | --- |
| `Id` | `Conoscope` |
| `name` | `Conoscope` |
| `version` | `1.4.6.1` |
| `dllpath` | `Conoscope.dll` |
| `requires` | Not declared in the current manifest |

## Positioning

The current implementation is split into three major layers:

- Image view layer: `ConoscopeView` renders images, channel switching, focus point circles, reference lines, polar angle circles, and local interaction.
- Main window layer: `ConoscopeWindow` owns Ribbon layout, current active view, capture, preprocessing, analysis, and export entries.
- Business service layer: `Application/Analysis`, `Application/Preprocess`, `Core`, and related folders handle calculation, preprocessing, export, and configuration.

Do not document it as a single-window utility. It now contains view, workflow, preprocessing, result display, and export chains.

## Main Entry Files

| File | Purpose |
| --- | --- |
| `ConoscopeWindow.xaml(.cs)` | Main window |
| `ConoscopeWindow.Ribbon.cs` | Ribbon organization |
| `ConoscopeWindow.HomeQuickControls.cs` | Current-view quick controls on the home tab |
| `ConoscopeWindow.AnalysisRibbon.cs` | Analysis buttons and state |
| `ConoscopeView.xaml(.cs)` | Single image view |
| `ConoscopeView.FocusPoint.cs` | Focus point circle rendering and sampling |
| `ConoscopeView.ReferenceAxis.cs` | Reference lines, polar angle circles, coordinate aids |
| `Application/Analysis/ConoscopeAnalysisWorkflow.cs` | Color gamut and contrast analysis workflow |
| `Application/Analysis/FocusPointMeasurementService.cs` | Focus point measurement service |
| `Application/Preprocess/ConoscopePreprocessPipeline.cs` | Image preprocessing pipeline |
| `Core/ConoscopeManager.cs` | Runtime manager |
| `Core/ConoscopeConfig.cs` | Plugin configuration |

## User-Visible Flow

The `VAM` entry in the Tools menu opens `ConoscopeWindow`. Users can import CVCIE images, open the observation camera, select models, and switch between multiple view tabs.

The home tab current-view controls follow the active tab:

- Switch display channel.
- Switch reference graphic mode.
- Edit reference radius or reference angle.
- Enter 3D, CIE, azimuth export, polar export, and advanced export.

When no view is active, the layout remains but interaction is disabled.

## Focus Points and Analysis

Conoscope uses local focus point logic inside the plugin. Each focus point is drawn as a circular overlay on the image and can be dragged or calculated from the context menu.

Color gamut analysis records snapshots for R, G, and B images, then calculates combined gamut against the selected standard gamut. Results open in `ColorGamutResultWindow`.

Contrast analysis records white and black field snapshots, then calculates per-focus-point white luminance, black luminance, and contrast in `ContrastResultWindow`.

## Preprocessing

Preprocessing code is concentrated in:

- `ConoscopePreprocessSettingsWindow.xaml(.cs)`
- `ConoscopePreprocessSettingsControl.xaml(.cs)`
- `Application/Preprocess/ConoscopePreprocessPipeline.cs`
- `Processing/Preprocess/DustRemovalProcessor.cs`
- `Processing/Preprocess/ImageFilterProcessor.cs`
- `Processing/Preprocess/XyzClampProcessor.cs`

Supported processing includes filtering, pseudo color, dust repair, thresholding, cropping, and XYZ clamp. The current view refreshes after preprocessing.

## MVS Camera Chain

The `MVS/` folder owns observation camera support:

- `MVCamera.cs`
- `MVSViewWindow.xaml(.cs)`
- `MVSViewManager.cs`
- `MVSGratingSettingsWindow.xaml(.cs)`
- `MVSGratingOverlayVisual.cs`

Treat this as Conoscope's internal observation/capture helper chain, not the same thing as Engine's general `DeviceCamera`.

## Build and Package

Build:

```powershell
dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64
```

PostBuild copies files to:

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Conoscope/
  Conoscope.dll
  manifest.json
  README.md
  CHANGELOG.md
```

Package:

```powershell
Scripts\package_plugin.bat Conoscope --no-upload
```

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Plugin loading | Check `manifest.json`, `dllpath`, and the Tool menu | The Tool menu shows `VAM`, and `ConoscopeWindow` opens |
| Delivery structure | Inspect the plugin folder after build or packaging | `Conoscope.dll`, `manifest.json`, `README.md`, and `CHANGELOG.md` exist; record MVS/native dependencies when the observation camera is used |
| Image open | Import a CVCIE file or field sample | `ConoscopeView` renders the image, and channel/reference overlays work |
| Current-view sync | Switch between image tabs and edit home quick controls | Active view, Ribbon state, and quick-control enabled state stay consistent |
| Focus point sampling | Add or drag a focus circle, then calculate from the context menu | Focus point values update, and circular overlays remain stable |
| Color gamut analysis | Record R/G/B snapshots, select a standard gamut, and calculate | `ColorGamutResultWindow` opens with overview and per-focus-point results |
| Contrast analysis | Record white and black fields, then calculate | `ContrastResultWindow` opens with white luminance, black luminance, and contrast |
| Preprocessing | Adjust filtering, dust repair, thresholding, cropping, or XYZ clamp | The current view refreshes, and before/after behavior can be reviewed |
| MVS observation camera | Open the MVS view on a machine with camera and driver | Camera enumeration, live preview, and grating overlay work; mark as unverified when hardware is unavailable |
| Result export | Run azimuth, polar, or advanced export | The exported file contains expected columns and focus point data |

## First Checks

| Symptom | Check first |
| --- | --- |
| Tool menu has no `VAM` | Plugin folder, `manifest.json` `Id/dllpath`, and whether `Conoscope.dll` was copied to host `Plugins/Conoscope/` |
| Window opens but no image appears | Sample file path, CVCIE format, `ConoscopeView` image loading chain, and FileIO boundary |
| Focus point values look wrong | Active view, display channel, focus circle radius, reference coordinates, and sample position |
| Color gamut or contrast result is empty | Whether R/G/B or white/black snapshots were fully recorded, and whether a standard gamut was selected |
| Home quick controls do not affect the image | Active tab synchronization, current-view binding, and Ribbon state refresh |
| Preprocessing has no visible effect | Whether `ConoscopePreprocessPipeline` is enabled, parameters are written, and the view refreshes |
| MVS preview is blank | `MvCameraControl.dll`, MVS driver, camera permission, cable, and `MVSViewManager`; do not debug it as Engine `DeviceCamera` |
| Export is missing fields | Export models, focus point snapshots, and result-window fields changed together |

## Handoff Notes

- Focus point logic is local to this plugin and is not the same as the generic Engine POI template.
- Color gamut and contrast result windows are independent result displays; do not push large result controls back into the main window.
- Keep home quick controls and view-local controls synchronized.
- If help entries change, update `Plugins/Conoscope/README.md` and `CHANGELOG.md`; the runtime help window reads them.
- If analysis fields change, verify CSV export, result windows, and batch record models.

## Continue Reading

- [Existing Plugin Field Acceptance And Handoff Checklist](../plugin-field-acceptance.md)
- [Plugin Capability & Handoff Matrix](../plugin-capability-matrix.md)
- [Plugin Runtime And Handoff Playbook](../plugin-handoff-playbook.md)
