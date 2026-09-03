# Repository scripts

This directory contains release wrappers, package/manifest tools, and source statistics utilities. Run commands from the repository root in PowerShell.

| Task | Entry points | Current guide |
| --- | --- | --- |
| Main application release | `release.bat` | [Build and release scripts](../docs/02-developer-guide/scripts/README.md) |
| Plugin, project, or Spectrum release | `package_plugin.bat`, `package_project.bat`, `Spectrum.bat` | [Package and dual-channel release](../docs/02-developer-guide/scripts/README.md#插件和项目包) |
| Package manifest validation and shared-file lists | `package_cvxp.py --validate-only`, `generate_shared_files.py` | [Validation and shared-file rules](../docs/02-developer-guide/scripts/README.md#选择入口) |
| Current worktree line counts | `count_code_lines.py` | [Code statistics](../docs/02-developer-guide/scripts/code-statistics.md#统计当前目录) |
| Git history data, PNG, and HTML | `generate_code_history_dashboard.py` | [History statistics and builder prerequisites](../docs/02-developer-guide/scripts/code-statistics.md#生成历史图表) |

Release wrappers write to the remote distribution service. Package creation may upload and delete local packages; use the guide to choose the correct entry point and understand failure behavior. `build.py`, `build_update.py`, and `verify_release.py` are main-release internals, not alternative release commands. In particular, do not execute `build_update.py` to request help: it enters package generation and upload.

Statistics commands have different inputs and dependencies: worktree counts use Python's standard library, while history generation needs Git and Pillow, plus an external portable-artifact builder for HTML. `--no-build` skips HTML only. Filtering, output paths, caches, metric definitions, and verification limits are maintained in the linked statistics guide.
