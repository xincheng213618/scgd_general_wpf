# ColorVision repository guidance

## Project facts

- ColorVision is a Windows WPF inspection platform. The main app targets `net10.0-windows` and primarily ships x64. Check `Directory.Build.props` and the owning project for target frameworks and platforms.
- Strong-name signing is conditional on `ColorVision.snk`. Do not disable it when the key exists.
- The application is modular: UI libraries live under `UI/`, engine code under `Engine/`, runtime plugins under `Plugins/`, and customer bundles under `Projects/`.

## Find and maintain project knowledge

- Start with the owning code, callers and tests. Read the relevant topic for cross-module contracts, compatibility and field behavior. When ownership is unclear, use `docs/knowledge/index.md`, `node docs/.vitepress/scripts/knowledge.mjs search "<question or symbol>" --limit 5`, or `node docs/.vitepress/scripts/knowledge.mjs impact "<repository-relative path>"`. Verify topic claims against current code and tests.
- Keep the owning topic and source references accurate. Update current facts in place. Put change and verification records in Git, CHANGELOG or the task report. Follow `docs/AGENTS.md` for docs edits. Accurate docs and mappings need no local regeneration for code-only work.
- Publishing, deleting data, controlling hardware, accessing credentials, committing and pushing require authorization from the task. Command examples supply invocation details only.

## Feedback retrieval

- For the latest feedback, run `pwsh -NoProfile -File .\Scripts\download_feedback.ps1 -Latest -AllowInsecureHttp` first for the current deployment. Use `-List` to list candidates and `-Machine <name>` to filter by machine. Report the selected machine, receive time and feedback ID from the script.
- Follow `docs/02-developer-guide/backend/feedback.md` for timestamp precedence, credentials, directory layouts and migration. Use feedback metadata rather than filesystem times, protect credentials, and require an explicit request to rename historical folders.

## Architecture boundaries

- Put device and service implementations under `Engine/ColorVision.Engine/Services/**`.
- Keep flow primitives in `Engine/FlowEngineLib/` and algorithm templates in `Engine/ColorVision.Engine/Templates/**`.
- Keep result pipelines distinct: Engine historical results use `IViewResult` and `IResultHandleBase`, discovered by `ResultHandleRegistry`; unified local algorithms emit neutral Geometry/Overlay artifacts rendered by `AlgorithmOverlayRenderer` and managed by `AlgorithmOverlayManager`. Do not add Engine DAO/handler dependencies to neutral algorithms. Customer judgment, exports, and protocol fields belong in `Projects/`; shared drawing infrastructure belongs under `UI/ColorVision.ImageEditor/Draw/**`. See `docs/04-api-reference/engine-components/result-handoff-chain.md` for the full contract.
- Use the metadata-driven PropertyGrid conventions (`Category`, `DisplayName`, `Description`, `PropertyEditorType`, and `PropertyVisibility`) instead of one-off editors where the existing system applies.
- Keep UI-to-Engine dependencies behind existing abstractions; avoid ad-hoc cross-layer calls.
- For Copilot configuration and instruction discovery, follow `docs/02-developer-guide/core-concepts/copilot-configuration.md`. Preserve ColorVision-owned runtime settings and scoped instruction discovery.
- When working in `Native/`, `Plugins/`, `Projects/`, `Web/`, or `docs/`, also read the nearest nested `AGENTS.md`. The closest file supplies the subsystem-specific rules.

## Build and verification

Use PowerShell from the repository root unless an owning guide specifies a different working directory. Before recursive delete or move, verify the resolved absolute target and use `-LiteralPath`.

```powershell
# Main application
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64

# Full release solution (run in Visual Studio Developer PowerShell)
dotnet restore .\build.sln
msbuild .\build.sln /m /p:Configuration=Release /p:Platform=x64

# Copilot managed test suite
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64

# UI managed test suite
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64
```

- Run the closest relevant build or tests with the existing configuration and platform. Follow `docs/02-developer-guide/testing.md` for regression coverage, deterministic tests and verification evidence. Treat production behavior and documented contracts as authoritative when a test is stale.
- Keep `.github/workflows/dotnet.yml` focused on Release/x64 builds and delivery contracts. Run managed, script, UI and performance tests locally.

## Release and packaging

- Before editing release metadata or running a wrapper, read `docs/02-developer-guide/scripts/README.md`. An explicitly named target wins. Otherwise publish each affected artifact via its own entry point, including the main app only when its shipped code is affected.
- Main releases use the entire current checkout and branch, including relevant pending work from other chats. Follow any explicitly requested scope or Git operation. `Scripts\release.bat` owns the main build, upload and acceptance.
- Default to the documented quick release. Only explicit “完整发布” selects expanded acceptance. Commit, tag or push only when explicitly requested.

## Code conventions

- Preserve the runtime dependencies declared by the actual projects and delivery packages, including vendor assets under `DLL/scgd_internal_dll/` and `OpenCvSharp5.runtime.win`. Current `CVCommCore.*` and `MQTTMessageLib.*` source types compile into `cvColorVision.dll`; retain matching standalone DLLs whenever a legacy or external plugin still references those assembly identities. See `docs/04-api-reference/engine-components/cvColorVision.md` for the namespace and assembly boundary.
- Use `CopyToOutputDirectory` for runtime configuration or assets when needed.
- Keep simple calls on one line when readable. Explain the impact of cross-module or protocol changes before making them.
- Do not add a forwarding overload merely to let one or two internal callers omit a result or replace an `out` value with `out _`. Keep one only when it is a genuine, reused public API shape.

## Completion criteria

- Check the requested behavior and final diff. Report verification evidence, specific blockers and any remaining gap.

## References

- Architecture: `docs/03-architecture/README.md`
- Extensibility: `docs/02-developer-guide/core-concepts/extensibility.md`
- Backend: `docs/02-developer-guide/backend/README.md`
- Build and release scripts: `docs/02-developer-guide/scripts/README.md`
