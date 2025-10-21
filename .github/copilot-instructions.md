# ColorVision guide for AI agents

Be immediately productive in this Windows-only WPF repo (net8.0-windows, x64). Follow the concrete patterns and paths below.

## Architecture (what goes where)
- Main app WPF: `ColorVision/` (+ UI libs under `UI/ColorVision.*`).
- Engine: `Engine/ColorVision.Engine` orchestrates devices, FlowEngine, Templates, MQTT, DB, OpenCV; uses `Engine/FlowEngineLib`, `Engine/cvColorVision`, `Engine/ColorVision.FileIO`.
- Plugins: drop-ins under `Plugins/` discovered at runtime; extend menus/tools/devices/algorithms.
- Projects: `Projects/Project*` customer-specific bundles. Docs: `docs/` (VitePress).

## Build/run/test (Windows)
- Global props: `Directory.Build.props` (signing ON, platforms x64/ARM64, TFMs net8.0-windows). Default x64.
- Build/run:
  - VS2022 open `scgd_general_wpf.sln`.
  - Or PowerShell: restore → build (`-p:Platform=x64`) → run `ColorVision/ColorVision.csproj`.
- External deps: keep `OpenCvSharp4.runtime.win` in output; Engine references `DLL/CVCommCore.dll`, `DLL/MQTTMessageLib.dll` (CopyLocal=true) — must exist at runtime.
- Tests: `Test/ColorVision.UI.Tests` (xUnit, WPF-bound). Run on Windows only. Clean with `./clear-bin.ps1`.

## Repo conventions
- Target `net8.0-windows` + `x64`; do not disable strong-name signing.
- MVVM UI with metadata attributes for the dynamic PropertyGrid: `Category`, `DisplayName`, `Description`, plus custom `PropertyEditorType`, `PropertyVisibility` (see `UI/ColorVision.UI/**`).
- Set `CopyToOutputDirectory` for assets/configs as needed (e.g., `ColorVision/log4net.config` uses PreserveNewest).
- Respect layering: UI ↔ Engine via abstractions; avoid ad-hoc cross-layer calls.

## Key integration points
- Device/Services: implement under `Engine/ColorVision.Engine/Services/**`; MQTT under `Engine/ColorVision.Engine/MQTT/**` and related services.
- Flow engine: primitives in `Engine/FlowEngineLib/`; algorithm Templates in `Engine/ColorVision.Engine/Templates/**`.
- Result overlays: implement `IViewResult` + `IResultHandleBase`; visuals in `UI/ColorVision.ImageEditor/Draw/**`. Examples: `Templates/*/Display*.xaml(.cs)`.
- Menus/Hotkeys/Settings: use managers in `UI/ColorVision.UI/**` (don’t wire manually).

## Plugin workflow (quick)
- Class Library `net8.0-windows`; add `<UseWPF>true</UseWPF>` if UI.
- Implement plugin entry (`IPlugin`/`IPluginBase` patterns; see `docs/extensibility/README.md`, `Plugins/**`).
- Post-build copy to `ColorVision/bin/<Config>/net8.0-windows/Plugins/<Name>/` so the app auto-discovers it.

## References
- Root: `README.md` (arch/features/plugins/property editor examples)
- Arch: `docs/architecture/README.md`  • Ext: `docs/extensibility/README.md`
- Updater: `ColorVisionSetup/` (.NET Framework 4.8) — orthogonal to Engine/UI
- Projects: `ColorVision/ColorVision.csproj`, `Engine/ColorVision.Engine/ColorVision.Engine.csproj`

If anything is unclear (e.g., which plugin interface to implement or where to hook a new result renderer), say which feature you’re building and we’ll point to the exact files.
