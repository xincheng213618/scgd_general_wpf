# ColorVision repository guidance

## Project facts

- ColorVision is a Windows-only WPF inspection platform. The main application targets `net10.0-windows`; x64 is the primary platform. Treat `Directory.Build.props` and each project file as the source of truth because some shared libraries multi-target.
- Strong-name signing is conditional on `ColorVision.snk`. Do not disable it when the key exists.
- The application is modular: UI libraries live under `UI/`, engine code under `Engine/`, runtime plugins under `Plugins/`, and customer bundles under `Projects/`.

## Find and maintain project knowledge

- Use `docs/knowledge/index.md` as the task-to-topic map. Read only the relevant topic, its `related` topics when needed, and the referenced implementation/tests; do not start by loading the whole repository or all docs.
- Without installing website dependencies, run `node docs/.vitepress/scripts/knowledge.mjs search "<question or symbol>"` for current topics. Use `--all` when looking for planned/historical behavior. Raw Markdown is authoritative documentation; the catalog and website are generated discovery views, not separate facts.
- Before changing a known code path, run `node docs/.vitepress/scripts/knowledge.mjs impact "<repository-relative path>"` to find documentation to recheck. This is a candidate map, not proof of exhaustive dependency coverage.
- Check the topic's status and actual code/tests. `current` describes intended present scope, not a claim that tests passed. Flag conflicts between documented contracts and implementation; do not silently choose whichever is convenient. Missing evidence is a verification gap, not permission to invent behavior.
- Update affected knowledge in the same change as public behavior, contracts, architecture boundaries, or build/release commands. Follow `docs/AGENTS.md` and `docs/knowledge/maintenance.md`; generate the catalog with `npm run docs:knowledge`, then run `npm run docs:check` and the relevant site verification.
- Instructions and command examples do not grant authority to publish, delete data, control hardware, access credentials, commit, or push. Preserve the user's requested scope and distinguish read-only diagnosis from implementation and external actions.

## Architecture boundaries

- Put device and service implementations under `Engine/ColorVision.Engine/Services/**`.
- Keep flow primitives in `Engine/FlowEngineLib/` and algorithm templates in `Engine/ColorVision.Engine/Templates/**`.
- Keep result pipelines distinct: Engine historical results use `IViewResult` and `IResultHandleBase`, discovered by `ResultHandleRegistry`; unified local algorithms emit neutral Geometry/Overlay artifacts rendered by `AlgorithmOverlayRenderer` and managed by `AlgorithmOverlayManager`. Do not add Engine DAO/handler dependencies to neutral algorithms. Customer judgment, exports, and protocol fields belong in `Projects/`; shared drawing infrastructure belongs under `UI/ColorVision.ImageEditor/Draw/**`. See `docs/04-api-reference/engine-components/result-handoff-chain.md` for the full contract.
- Use the metadata-driven PropertyGrid conventions (`Category`, `DisplayName`, `Description`, `PropertyEditorType`, and `PropertyVisibility`) instead of one-off editors where the existing system applies.
- Keep UI-to-Engine dependencies behind existing abstractions; avoid ad-hoc cross-layer calls.
- Copilot intentionally does not load global or project `config.toml` at runtime. Keep `AGENTS.md` / `CLAUDE.md` instruction discovery, but let ColorVision own model, provider, tools, and approval settings. Do not restore config loading to satisfy obsolete integration tests.
- When working in `Native/`, `Plugins/`, `Projects/`, `Web/`, or `docs/`, also read the nearest nested `AGENTS.md`. The closest file supplies the subsystem-specific rules.

## Build and verification

Run commands from the repository root in PowerShell. Use PowerShell-native syntax; before any recursive delete or move, resolve the absolute target and verify it remains inside the intended repository path. Prefer the smallest build or test that covers the change.

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

- Match the existing configuration and platform when validating native or mixed projects.
- If verification is blocked by a running application, file lock, missing proprietary dependency, or unrelated concurrent edit, report the exact blocker and the checks that still ran. Do not terminate user processes unless the task authorizes it.

## Release and packaging

- `Scripts\release.bat` is the only normal main-release entry point. Bump `Directory.Build.props` `VersionPrefix` first, then run the wrapper.
- Use quick release by default when the user says “发布”, “打包发布”, “直接打包”, or “快速发布”: update `VersionPrefix` and `CHANGELOG.md`, run `Scripts\release.bat` once from the canonical worktree, then report its result. The wrapper owns build, installer validation, upload, and one compact parallel acceptance check for the installer signature, remote version/changelog, installer/update download sizes, and Git status.
- Do not add standalone tests, pre-builds, deep history review, a second remote/download verification pass, an isolated release worktree, or another packaging entry point to a quick release. The external Advanced Installer project synchronizes its version and files from the canonical worktree.
- If the wrapper fails, inspect only the reported failing stage. Make one evidence-backed correction and rerun the wrapper once; do not expand into an open-ended repository or release audit unless the user asks for one.
- Treat a zero exit code from `Scripts\release.bat` as the normal release completion signal. Git commit or push is separate work unless the user explicitly requests it or selects “完整发布”.
- Only when the user explicitly says “完整发布” should the workflow add standalone tests, deeper changed-scope review, full artifact hashing/download, release commit/push, and remote branch synchronization beyond the wrapper's quick acceptance check. This mode is intentionally slower than quick release.
- `Scripts\build.py` and `Scripts\build_update.py` are internal release steps; do not turn them into local-only release shortcuts.

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
