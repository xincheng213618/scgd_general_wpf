# Scripts quick notes

Full reader-facing guide: `docs/02-developer-guide/scripts/README.md`.

## Safe entry points

| Task | Command |
| --- | --- |
| Main application release | `Scripts\release.bat` |
| Publish plugin package | `Scripts\package_plugin.bat <PluginName>` |
| Publish project package | `Scripts\package_project.bat <ProjectName>` |
| Publish Spectrum ZIP + plugin | `Scripts\Spectrum.bat --release-notes "<notes>"` |
| Publish an existing output directory | `py Scripts\package_cvxp.py --src-dir <output-dir>` |
| Validate plugin manifest only | `py Scripts\package_cvxp.py --project-file <plugin.csproj> --validate-only` |
| Refresh host shared-file manifests | `py Scripts\generate_shared_files.py` |
| Verify manifests against the current host output | `py Scripts\generate_shared_files.py --check` |
| Count code, comment, and blank lines | `py Scripts\count_code_lines.py` |
| Generate the interactive code-history dashboard | `py Scripts\generate_code_history_dashboard.py` |

`build.py` and `build_update.py` are release internals. Do not use them as normal manual release entry points; `build_update.py` executes package generation and upload when run.

The code-line counter respects `.gitignore` and has no third-party dependencies. To save a reusable machine-readable report, run `py Scripts\count_code_lines.py --format json --output code-lines.json`.

The code-history dashboard follows the current branch's first-parent Git history and writes a self-contained offline page to `.codex-artifacts\code-history-dashboard\index.html` plus a 1080×1440 social-sharing image at `.codex-artifacts\code-history-dashboard\share-card.png`. It includes exact weekly code/content snapshots; a day/week/month/half-year/year selector for additions, deletions, total change, and net growth; natural-day average additions/deletions/change; paired rewrite/refactor estimates; major code-size jumps; natural data-detected pace changes; nearby `CHANGELOG.md` release context; and directory/language activity. It deliberately keeps raw commit detail out of the dashboard so the primary analysis stays compact. It does not hard-code AI tool eras: sustained changes are detected by comparing the median change volume of the eight complete weeks before and after a candidate point. Re-run the command whenever history changes; add `--open` to open the result immediately, or pass `--share-card <path>` to choose the PNG output. The ignored `.codex-artifacts\code-history-dashboard\history-cache.json` cache reuses immutable commit nodes, Git blob counts, exact weekly snapshots, and unchanged HEAD file counts, so subsequent runs only parse new commits and count unseen content. Use `--refresh-cache` after a manual cache reset, or `--verify` when a slower browser-based portable-page verification is required. PNG generation requires Pillow.

The main application and ServiceHost inherit the same `VersionPrefix` from `Directory.Build.props`; a normal release has only this one core version source. Every incremental package carries the complete `ServiceHost/` runtime so ZIP deployments can install the service into an empty ProgramData directory.

For manifest-based packages, `manifest.id` is the marketplace/package/install identity and `dllpath` identifies the primary assembly. The project name, assembly name, and plugin ID do not need to match.

## Spectrum dual release

`Scripts\Spectrum.bat` remains the normal Spectrum release entry point. It builds both `Spectrum<four-part-version>.zip` for standalone installations and `Spectrum-<four-part-version>.cvxp` for ColorVision plugin installations. The script synchronizes the plugin manifests to the PE file version, then signs the canonical standalone manifest with the configured RSA certificate before any remote write.

The upload order is the plugin package file, the standalone atomic publish endpoint, and finally the plugin `LATEST_RELEASE`. The script then verifies both latest feeds, re-downloads the plugin package to compare its byte length and SHA-256, and downloads the standalone ZIP through an HTTP Range request for the same check. Any failed upload or verification exits nonzero and keeps both local packages; the local `.cvxp` is deleted only after the complete remote verification succeeds.

Formal `--upload` requires the `CN=xincheng` certificate with thumbprint `0AFB92F7CF8B33F13C931B327B1BE5DC773F30FA` and its RSA private key in `Cert:\CurrentUser\My`. The private key is never exported. To build local packages without signing or publishing, run:

```powershell
py Scripts\build_spectrum.py --release-notes "local package"
```

The local-only command prints that no signed release manifest was published.

## Upload environment

Use environment variables for remote uploads:

```powershell
$env:COLORVISION_UPLOAD_URL = "http://<host>:<port>"
$env:COLORVISION_UPLOAD_FOLDER = "ColorVision"
$env:COLORVISION_UPLOAD_USERNAME = "<user>"
$env:COLORVISION_UPLOAD_PASSWORD = "<password>"
```

Optional proxy flag:

```powershell
$env:COLORVISION_UPLOAD_USE_SYSTEM_PROXY = "1"
```

Do not put real credentials in docs or checked-in command examples.

The backend HTTP upload endpoint is the only release distribution channel. A main release uploads the installer and `CHANGELOG.md` first, then updates `LATEST_RELEASE`; it does not copy artifacts to WeDrive or Baidu Cloud. The local `Desktop\History` directory remains an incremental-package build baseline, not a distribution channel.

## Current script map

| Script | Purpose |
| --- | --- |
| `release.bat` | Normal release wrapper |
| `build.py` | Release internal: main installer build/upload |
| `build_update.py` | Release internal: incremental package build/upload |
| `package_cvxp.py` | Plugin manifest validation plus `.cvxp` package creation, upload, and cleanup |
| `package_plugin.bat` | Repo plugin wrapper around `package_cvxp.py --build` |
| `package_project.bat` | Repo project wrapper around `package_cvxp.py --build` |
| `generate_shared_files.py` | Generate or set-check the repository and Plugin Kit `shared_files.json` mirrors from one host output scan |
| `build_spectrum.py` | Spectrum ZIP + `.cvxp` build, signed dual-feed publish, and remote verification |
| `backend_client.py` | Shared upload/auth/preflight client |
| `count_code_lines.py` | Dependency-free code, comment, and blank-line report |
| `generate_code_history_dashboard.py` | Generate the offline Git code-history dashboard |

If a file is not present in `Scripts/`, do not document it as an active entry point.
