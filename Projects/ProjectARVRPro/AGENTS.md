# ProjectARVRPro guidance

This file adds to both the repository-root and `Projects/AGENTS.md` guidance.

- Treat `ProjectARVRPro.csproj` `VersionPrefix` as the manual plugin version source. Do not manually synchronize `manifest.json`; `Scripts/package_cvxp.py` updates its `version` from the built primary DLL during packaging.
- The main application version in `Directory.Build.props` is separate. When a task releases both products, update and verify both version sources explicitly.
- Build `Projects/ProjectARVRPro/ProjectARVRPro.csproj` and run `Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj` for relevant changes.
