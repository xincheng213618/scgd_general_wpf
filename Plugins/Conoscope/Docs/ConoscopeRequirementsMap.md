# Conoscope Requirements Map

This file maps the product requirements to implementation areas so future changes do not require searching through large XAML code-behind files.

## Feature Status

| ID | Area | Current Status | Main Gap | Suggested Module |
| --- | --- | --- | --- | --- |
| 1 | UI function zones | Partial. Top tabs exist for home, capture, preprocess, analysis, window. | Need explicit File/Camera/Analysis/Preprocess/Calibration grouping and a cleaner command model. | `ConoscopeWindow.*.cs`, `ConoscopeWindow.xaml` |
| 2 | Model selection | Implemented for VA60/VA80. | Keep model-specific UI visibility centralized. | `Core/ConoscopeModelProfile.cs`, `ConoscopeWindow.Model.cs` |
| 3 | Observation camera | Mostly implemented for VA60: open button, draggable/resizable window, size dropdown, center/scale config. | Need verify VA80 hidden path, center marker workflow, and make size/center config easier to find. | `MVS/*`, `Core/ConoscopeModelProfile.cs` |
| 4 | 3D image display | Implemented through current view 3D window. | Verify pan/rotate UX and expose from analysis ribbon. | `ConoscopeView.Tools.cs`, `Window3D` |
| 5 | Color difference | Mostly implemented: uv formula, fixed illuminants, image center, custom, reference image. | Need make reference image capture workflow more explicit in top Analysis area. | `ConoscopeView.ColorDifference.cs`, `Core/ConoscopeColorimetry.cs` |
| 6 | ND switching | Partial: set/read ND and template binding exist. | Need ND dropdown values `0/8/64/1000` and increment/decrement controls. | `ConoscopeWindow.Capture.cs` |
| 7 | Measurement spot selection | Partial in observation camera grating UI. | Need main measurement spot selector and shared enum/options `3/2/1/0.5mm`. | `MVS/*`, `Core/ConoscopeModelProfile.cs` |
| 8 | Template auto matching | Implemented by ND-calibration binding. | Need user-visible binding summary/status. | `ConoscopeWindow.Capture.cs` |
| 9 | Contrast test | Partial/manual window exists. | Need black/white capture buttons and calculate flow from measured data. | `Analysis/ContrastTestWindow.*`, `ConoscopeWindow.Analysis.cs` |
| 10 | Color gamut | Partial/manual calculation exists with standard gamut list. | Need R/G/B capture buttons and measured-image-to-gamut flow. | `Analysis/ColorGamutWindow.*`, `ConoscopeWindow.Analysis.cs` |
| 11 | Focus point settings | Partial: reference line/circle can be adjusted. | Need focus point object with live coordinate, azimuth/polar display, adjustable point size, conditional display after calculations. | `ConoscopeView.FocusPoint.cs` |
| 12 | CIE diagram display | Implemented using shared CIE diagram control. | Need selected focus point highlighting and standard gamut triangle toggles in Conoscope workflow. | `ColorVision.ImageEditor/Cie/*`, `ConoscopeView.Cie.cs` |
| 13 | XYZ/xy/contrast/difference/gamut display | Partial: channel dropdown supports XYZ/xy/uv/difference. | Need contrast/gamut result display only after corresponding calculation. | `ConoscopeView.Rendering.cs`, `ConoscopeView.AnalysisResults.cs` |
| 14 | Data export | Partial: selected channel CSV export exists. | Need export dataset selector and include calculated contrast/difference/gamut results when available. | `Core/ConoscopeExportService.cs`, `ConoscopeView.Export.cs` |

## Target Code Layout

`ConoscopeWindow` should only coordinate window-level commands:

- `ConoscopeWindow.xaml.cs`: construction, theme, lifetime, high-level initialization.
- `ConoscopeWindow.Documents.cs`: AvalonDock document/view management.
- `ConoscopeWindow.Preprocess.cs`: top ribbon preprocess and pseudo-color preset controls.
- `ConoscopeWindow.Capture.cs`: flow templates, cameras, ND switching, calibration binding.
- `ConoscopeWindow.Analysis.cs`: contrast, gamut, 3D, CIE, export command routing.
- `ConoscopeWindow.Model.cs`: VA60/VA80 selection and model-dependent visibility.

`ConoscopeView` should be split by current image behavior:

- `ConoscopeView.xaml.cs`: construction, data context, lifetime.
- `ConoscopeView.Rendering.cs`: channel display, pseudo-color, legend, zoom toolbar.
- `ConoscopeView.Preprocess.cs`: filter/dust/clamp configuration and apply operation.
- `ConoscopeView.ColorDifference.cs`: reference source, uv difference, reference image capture.
- `ConoscopeView.ReferenceCurves.cs`: azimuth/polar sampling and ScottPlot updates.
- `ConoscopeView.FocusPoint.cs`: focus point display/editing and CIE point sync.
- `ConoscopeView.Export.cs`: CSV/export actions.
- `ConoscopeView.Tools.cs`: full screen, 3D, CIE window commands.

Shared logic should live under `Core/` and avoid UI dependencies where possible:

- `ConoscopeColorimetry`: XYZ/xy/uv/CCT/color difference math.
- `ConoscopePseudoColorRenderer`: channel mat to pseudo-color bitmap rendering.
- `ConoscopePreprocessService`: clamp/filter/dust removal.
- `ConoscopeExportService`: export matrix/result datasets.
- `ConoscopeAnalysisResults`: stores contrast/color difference/gamut results for display/export.
