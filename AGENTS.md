# ColorVision repository guidance

## Project facts

- ColorVision is a Windows-only WPF inspection platform. The main application targets `net10.0-windows`; x64 is the primary platform. Treat `Directory.Build.props` and each project file as the source of truth because some shared libraries multi-target.
- Strong-name signing is conditional on `ColorVision.snk`. Do not disable it when the key exists.
- The application is modular: UI libraries live under `UI/`, engine code under `Engine/`, runtime plugins under `Plugins/`, and customer bundles under `Projects/`.

## Architecture boundaries

- Put device and service implementations under `Engine/ColorVision.Engine/Services/**`.
- Keep flow primitives in `Engine/FlowEngineLib/` and algorithm templates in `Engine/ColorVision.Engine/Templates/**`.
- Implement result overlays through `IViewResult` and `IResultHandleBase`; drawing infrastructure belongs under `UI/ColorVision.ImageEditor/Draw/**`.
- Use the metadata-driven PropertyGrid conventions (`Category`, `DisplayName`, `Description`, `PropertyEditorType`, and `PropertyVisibility`) instead of one-off editors where the existing system applies.
- Keep UI-to-Engine dependencies behind existing abstractions; avoid ad-hoc cross-layer calls.
- When working in `Native/`, `Plugins/`, `Projects/`, `Web/`, or `docs/`, also read the nearest nested `AGENTS.md`. The closest file supplies the subsystem-specific rules.

## Build and verification

Run commands from the repository root in PowerShell. Use PowerShell-native syntax; before any recursive delete or move, resolve the absolute target and verify it remains inside the intended repository path. Prefer the smallest build or test that covers the change.

```powershell
# Main application
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64

# Full release solution (run in Visual Studio Developer PowerShell)
dotnet restore .\build.sln
msbuild .\build.sln /m /p:Configuration=Release /p:Platform=x64

# Main managed test suite
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64
```

- Match the existing configuration and platform when validating native or mixed projects.
- If verification is blocked by a running application, file lock, missing proprietary dependency, or unrelated concurrent edit, report the exact blocker and the checks that still ran. Do not terminate user processes unless the task authorizes it.

## Release and packaging

- `Scripts\release.bat` is the only normal main-release entry point. Bump `Directory.Build.props` `VersionPrefix` first, then run the wrapper.
- `Scripts\build.py` and `Scripts\build_update.py` are internal release steps; do not turn them into local-only release shortcuts.
- A local installer, zip, or update package is not proof of a completed main release. Verify the upload, remote release metadata, and a downloadable artifact before reporting success.

## Code conventions

- Preserve required runtime dependencies such as `DLL/CVCommCore.dll`, `DLL/MQTTMessageLib.dll`, and `OpenCvSharp4.runtime.win` in the relevant output.
- Use `CopyToOutputDirectory` for runtime configuration or assets when needed.
- Optimize for direct, maintainable code rather than line count. Keep simple calls on one line; split only when it materially improves readability.
- Do not add a forwarding overload merely to let one or two internal callers omit a result or replace an `out` value with `out _`. Keep one only when it is a genuine, reused public API shape.

## Completion criteria

- Confirm the requested behavior, run the closest relevant build/tests, inspect the final diff for scope and accidental artifacts, and report concrete evidence plus any remaining verification gap.

## References

- Architecture: `docs/03-architecture/README.md`
- Extensibility: `docs/02-developer-guide/core-concepts/extensibility.md`
- Backend: `docs/02-developer-guide/backend/README.md`
- Build and release scripts: `docs/02-developer-guide/scripts/README.md`
