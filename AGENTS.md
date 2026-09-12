# ColorVision repository guidance

## Project facts

- ColorVision is a Windows-only WPF inspection platform. The main application targets `net10.0-windows`; x64 is the primary platform. Treat `Directory.Build.props` and each project file as the source of truth because some shared libraries multi-target.
- Strong-name signing is conditional on `ColorVision.snk`. Do not disable it when the key exists.
- The application is modular: UI libraries live under `UI/`, engine code under `Engine/`, runtime plugins under `Plugins/`, and customer bundles under `Projects/`.

## Find and maintain project knowledge

- For unfamiliar or cross-module work, use `docs/knowledge/index.md` or local search to locate the owning topic. When the owner is already known, start with its relevant contract, implementation and tests; do not repeat a whole-repository discovery pass or read every `related` topic.
- Without installing website dependencies, run `node docs/.vitepress/scripts/knowledge.mjs search "<question or symbol>"` for current topics. Use `--all` when looking for planned/historical behavior. Raw Markdown is authoritative documentation; the catalog and website are generated discovery views, not separate facts.
- Use `node docs/.vitepress/scripts/knowledge.mjs impact "<repository-relative path>"` when a behavior/contract change has unclear documentation ownership, or when moving/deleting referenced source or test paths. It finds review candidates, not a complete dependency graph; it is not mandatory for every internal code edit.
- Check the topic's status and actual code/tests. `current` describes intended present scope, not a claim that tests passed. Flag conflicts between documented contracts and implementation; do not silently choose whichever is convenient. Missing evidence is a verification gap, not permission to invent behavior.
- Update the owning topic when public behavior, contracts, architecture boundaries, operation steps or build/release commands change. Internal refactoring and fixes that restore an unchanged contract need no prose update only when existing knowledge, implementation entry points and verification guidance remain accurate; otherwise correct the affected statements or references. Preserve design reasons, compatibility rules and verification methods rather than narrating implementation edits.
- Follow the change-scoped validation table in `docs/knowledge/maintenance.md` when documentation changes. Regenerate the catalog only when its inputs change. Code-only work does not require local knowledge generation or a website build; existing CI checks still apply.
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
- Treat current production behavior and explicit documented contracts as authoritative when a test disagrees. Never change product code solely to satisfy a stale test.
- Do not loosen arbitrary timing, pixel, iteration-count, or scheduler thresholds to make a test pass. A passing tolerance may remain when it represents a confirmed product contract; once a temporary threshold or dispatcher-timing test fails after business behavior changes, remove or rewrite that test around deterministic behavior instead of accommodating it in production code.
- Keep GitHub Actions focused on Release/x64 build and delivery-contract verification. Do not run managed, script, UI, or performance test suites in `.github/workflows/dotnet.yml`; run the relevant suites locally while developing or reviewing the affected code.

## Release and packaging

- Choose the artifact from the active task first: an explicitly named target wins; after work scoped to one `Plugins/<Name>/` or `Projects/<Name>/`, bare “发布” publishes only that artifact using its nearest `AGENTS.md`.
- `Scripts\release.bat` is the only normal main-release entry point. For main-app/repository-wide “发布”, “打包发布”, “直接打包”, or “快速发布”, use an explicitly requested version when provided; otherwise increment the final numeric component of the current local `Directory.Build.props` `VersionPrefix` by one. Do not query the remote version first.
- Keep quick-release preparation minimal: update `VersionPrefix`, replace the root `CHANGELOG.md` with one section for the current release containing one to three short user-facing items, then run `Scripts\release.bat` once and report its result. The immutable legacy archive in `docs/_history/CHANGELOG.md` is for internal review only; do not update it during routine releases. It is excluded from the public documentation build and is not uploaded as the main application changelog. Omit tests, documentation, internal implementation details, file/class names, refactoring mechanics, and commit-by-commit narration from new public release notes.
- The wrapper owns build, installer validation, upload and compact parallel acceptance (signature, remote version/changelog, installer/update download sizes, Git status). Do not add standalone tests, pre-builds, knowledge/impact searches, deep diff or history review, duplicate remote/download checks, an isolated release worktree, or another packaging entry point. `Scripts\build.py` and `Scripts\build_update.py` are internal steps, never local-only shortcuts.
- A quick release does not create commits or tags and does not push. Perform those actions only when the user explicitly requests them.
- If the wrapper fails, inspect only that stage, make one evidence-backed correction, and rerun once; expand the audit only if requested.
- Only explicit “完整发布” adds standalone tests, deeper changed-scope review, full artifact hashing/download, and remote branch/tag synchronization beyond quick acceptance.

## Code conventions

- Preserve the runtime dependencies declared by the actual projects and delivery packages, including vendor assets under `DLL/scgd_internal_dll/` and `OpenCvSharp4.runtime.win`. Current `CVCommCore.*` and `MQTTMessageLib.*` source types compile into `cvColorVision.dll`; retain matching standalone DLLs whenever a legacy or external plugin still references those assembly identities. See `docs/04-api-reference/engine-components/cvColorVision.md` for the namespace and assembly boundary.
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
