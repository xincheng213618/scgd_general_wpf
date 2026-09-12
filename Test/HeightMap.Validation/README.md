# Height-map desktop validation host

An opt-in local WPF executable for inspecting `Window3D` and reproducing the saved Media3D baseline. It is not part of CI and does not change application settings. All configuration is held in memory. No source image is modified. It starts its own window with `ShowActivated=false`, drives its own camera, writes results, and closes itself; it does not send desktop input.

From the repository root in PowerShell:

```powershell
dotnet build .\Test\HeightMap.Validation\HeightMap.Validation.csproj -c Release -p:Platform=x64 -p:GeneratePackageOnBuild=false
$exe = (Resolve-Path .\Test\HeightMap.Validation\bin\x64\Release\net10.0-windows\HeightMap.Validation.exe).Path
& $exe --mode legacy --source synthetic-smooth --target 512 --seconds 12 --output artifacts/heightmap-validation/legacy-smooth
& $exe --mode current --source synthetic-smooth --target 1536 --interaction 512 --seconds 12 --output artifacts/heightmap-validation/current-smooth
```

Options are `--name value` pairs. For real data, replace `synthetic-smooth` with a fully qualified image path. Built-in deterministic images include `synthetic-smooth`, `synthetic-detail`, and `synthetic-portrait`. WPF bitmap decoders handle ordinary images; Gray32Float TIFF follows the product's existing Gray16 display conversion. The small CVRAW reader uses the repository FileIO header/data reader and supports interleaved 8/16-bit, one/three-channel data, swapping BGR16 to WPF RGB48. CVCIE is intentionally not decoded here: its displayed layer selection belongs to the Engine opener, so it must be tested through that opener.

If an interactive validation window is holding the standard output DLLs, build with `-p:ValidationOutputPath=<absolute scratch directory>` to give only this host a separate output directory, leaving the existing window running. The executable is placed in that directory's target-framework subdirectory.

- `--mode legacy|current`: saved Media3D window or current production window.
- `--target`: legacy sample budget, or current detail budget.
- `--interaction`: current cached interaction budget, default 512.
- `--adaptive true|false`: current automatic interaction detail, default true.
- `--seconds`: animated measurement duration, default 12.
- `--hold`: additional seconds to keep the window open for real UI inspection after measurement; input should be performed through the computer-use skill.
- `--export true`: additionally exercises the current geometry-to-OBJ pipeline into the output directory, including texture/material sidecars. It bypasses the save dialog and does not replace separate UI acceptance.
- `--inspect-model <path>`: skips window creation and reimports an existing model through the product's Assimp loader; writes `reimport.json` with geometry, material, texture and missing-texture statistics.
- `--verify-reload true`: after initial current-window loading, enters the cached interaction mesh and awaits `ReloadAsync` (30-second limit), then verifies that the active sample is the new full-detail sample, both interaction flags are false, and only the detail model renders. Writes `reload-contract.json`, closes the window, and skips the performance/screenshot phase. Use distinct budgets, for example `--mode current --target 1536 --interaction 512`.

`metrics.json` contains input size/format, decoding and window-ready wall time, sampling/build data, 50 hover ray queries, process CPU time/memory, composition callback intervals, actual DirectX adapter, and supported Helix render statistics. `initial.png` captures the window content; current runs also use the production GPU screenshot API for `gpu-initial.png`. `rotated.png` records the end of the trajectory. Legacy `reset.png` is captured after reset, before timing: its initial auto-zoom can otherwise frame corner axes before asynchronous geometry arrives.

## Baseline provenance and limits

`Legacy/` preserves the 3D implementation at repository commit `58980da20ae4f0d56cb3a77a7b7c36c818ae57d3`. The original seven files were copied before production editing and verified against their Git HEAD blob hashes. The harness copy changes only namespace/resource qualification, replaces persisted configuration with defaults, and adds timing/drive access. `Window3D.Validation.cs` supplies the trajectory and sampled hit queries. The extra source is deliberate baseline evidence, not a second product renderer. Resource images and strings remain referenced from ImageEditor. An optional `-p:LegacyBinaryDirectory=<existing application binary directory>` can build the baseline while production files are being edited; it only supplies references, not baseline geometry or interaction logic. Normal runs use project references.

The comparison uses the same input, logical window size (1280 × 820), height scale (100), colormap (jet), measurement duration, and bounded angular trajectory. The product change also changes panel layout, field of view, initial fit and model rotation versus camera orbit; therefore it is a product-experience comparison, not proof of an isolated renderer speed multiplier. Actual viewport dimensions are recorded. Measurements are from a normal active desktop and can include unrelated scheduler/load variation.

`CompositionTarget.Rendering` intervals measure WPF/dispatcher pacing, not actual GPU frames or present latency. Process CPU reports both percentage of one logical core and total machine percentage. `meshCollectionsMs` includes the old worker queue and UI continuation; separate worker array/collection timings exclude that wait. Current `BuildMilliseconds` is the renderer's own preparation wall time, not a directly equivalent old phase.

Helix `LatencyStatistics` is a rolling mean of CPU-side wall time from per-frame update through rendering, `Present`, and `PostRender`, as verified in [the v3.1.2 implementation](https://github.com/helix-toolkit/helix-toolkit/blob/v3.1.2/Source/HelixToolkit.SharpDX/Render/RenderHost/DX11RenderHostBase.cs#L719-L802). It can include synchronization and cannot split CPU submission from GPU waiting. No GPU timestamps, ETW present tracing, input-to-display latency, or low-end GPU acceptance are claimed.
