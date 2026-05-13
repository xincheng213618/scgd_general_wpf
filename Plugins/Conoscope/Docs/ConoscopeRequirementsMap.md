# Conoscope Requirements Map

This file maps the product requirements to implementation areas so future changes do not require searching through large XAML code-behind files.

## Feature Status

| ID | Area | Current Status | Main Gap | Suggested Module |
| --- | --- | --- | --- | --- |
| 1 | UI function zones | Implemented. Top tabs now cover home, capture, preprocess, analysis, window, and home includes active-view quick controls. | Continue trimming duplicated controls between ribbon and per-view editors. | `ConoscopeWindow.*.cs`, `ConoscopeWindow.xaml` |
| 2 | Model selection | Implemented for VA60/VA80. | Keep model-specific UI visibility centralized. | `Core/ConoscopeModelProfile.cs`, `ConoscopeWindow.Model.cs` |
| 3 | Observation camera | Mostly implemented for VA60: open button, draggable/resizable window, size dropdown, center/scale config. | Need verify VA80 hidden path, center marker workflow, and make size/center config easier to find. | `MVS/*`, `Core/ConoscopeModelProfile.cs` |
| 4 | 3D image display | Implemented through current view 3D window and analysis ribbon entry. | Verify pan/rotate UX only; entry point is already wired. | `ConoscopeWindow.Analysis.cs`, `ConoscopeView.Export.cs` |
| 5 | Color difference | Mostly implemented: uv formula, fixed illuminants, image center, custom, reference image. | Need make reference image capture workflow more explicit in top Analysis area. | `ConoscopeView.ColorDifference.cs`, `Core/ConoscopeColorimetry.cs` |
| 6 | ND switching | Partial: set/read ND and template binding exist. | Need ND dropdown values `0/8/64/1000` and increment/decrement controls. | `ConoscopeWindow.Capture.cs` |
| 7 | Measurement spot selection | Partial in observation camera grating UI. | Need main measurement spot selector and shared enum/options `3/2/1/0.5mm`. | `MVS/*`, `Core/ConoscopeModelProfile.cs` |
| 8 | Template auto matching | Implemented by ND-calibration binding. | Need user-visible binding summary/status. | `ConoscopeWindow.Capture.cs` |
| 9 | Contrast test | Implemented. Main ribbon records white/black batches from the active view and opens a dedicated result window; legacy manual window remains for compatibility. | Consider exporting result-window data directly. | `Analysis/ContrastResultWindow.*`, `ConoscopeWindow.AnalysisRibbon.cs` |
| 10 | Color gamut | Implemented. Main ribbon records R/G/B batches from the active view and opens a dedicated gamut result window. | Consider adding direct result export. | `Analysis/ColorGamutResultWindow.*`, `ConoscopeWindow.AnalysisRibbon.cs` |
| 11 | Focus point settings | Mostly implemented. Local focus circles can be drawn, sampled, and reused by gamut/contrast calculations without engine-side filtering. | Size presets and richer coordinate readback can still be expanded. | `ConoscopeView.FocusPoint.cs`, `Analysis/MeasurementCaptureModels.cs` |
| 12 | CIE diagram display | Implemented using shared CIE diagram control and the gamut result window. | Could add stronger cross-highlighting back to the source view. | `Analysis/ColorGamutResultWindow.*`, `ColorVision.ImageEditor/Cie/*` |
| 13 | XYZ/xy/contrast/difference/gamut display | Implemented. View-level channel display stays in the main view, while contrast/gamut results live in separate windows after calculation. | Calculated result export is still limited. | `ConoscopeView.Display.cs`, `ConoscopeWindow.AnalysisRibbon.cs` |
| 14 | Data export | Partial: selected channel CSV export exists. | Need export dataset selector and include calculated contrast/difference/gamut results when available. | `Core/ConoscopeExportService.cs`, `ConoscopeView.Export.cs` |

## Target Code Layout

`ConoscopeWindow` currently coordinates window-level commands through these partial files:

- `ConoscopeWindow.xaml.cs`: construction, theme, lifetime, high-level initialization.
- `ConoscopeWindow.Documents.cs`: AvalonDock document/view management.
- `ConoscopeWindow.Preprocess.cs`: top ribbon preprocess and pseudo-color preset controls.
- `ConoscopeWindow.Capture.cs`: flow templates, cameras, ND switching, calibration binding.
- `ConoscopeWindow.Analysis.cs`: 3D, CIE and export command routing.
- `ConoscopeWindow.AnalysisRibbon.cs`: gamut/contrast recording, calculation and result-window state.
- `ConoscopeWindow.HomeQuickControls.cs`: active-view quick control bridge for home ribbon.
- `ConoscopeWindow.Help.cs`: help window entry points from the window tab.

`ConoscopeView` is now split by current image behavior in these files:

- `ConoscopeView.xaml.cs`: construction, data context, lifetime.
- `ConoscopeView.Display.cs`: channel display refresh and display-channel synchronization.
- `ConoscopeView.Preprocess.cs`: filter/dust/clamp configuration and apply operation.
- `ConoscopeView.ColorDifference.cs`: reference source, uv difference, reference image capture.
- `ConoscopeView.ReferenceAxis.cs`: reference mode/value synchronization and axis quick controls.
- `ConoscopeView.ReferencePlot.cs`: azimuth/polar sampling and plot updates.
- `ConoscopeView.FocusPoint.cs`: local focus circle display, editing and measurement entry points.
- `ConoscopeView.WindowQuickControls.cs`: minimal state API exposed to the window-level ribbon.
- `ConoscopeView.Export.cs`: CSV/export actions.

Shared calculation and result plumbing now also lives under `Analysis/`:

- `MeasurementCaptureModels.cs`: active-view focus-point snapshots and batch calculators.
- `ColorGamutResultWindow.*`: gamut result presentation.
- `ContrastResultWindow.*`: contrast result presentation.

Shared logic should live under `Core/` and avoid UI dependencies where possible:

- `ConoscopeColorimetry`: XYZ/xy/uv/CCT/color difference math.
- `ConoscopePseudoColorRenderer`: channel mat to pseudo-color bitmap rendering.
- `ConoscopePreprocessService`: clamp/filter/dust removal.
- `ConoscopeExportService`: export matrix/result datasets.
- `ConoscopeAnalysisResults`: stores contrast/color difference/gamut results for display/export.
