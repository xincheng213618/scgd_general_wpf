# ProjectARVRPro guidance

This file adds to both the repository-root and `Projects/AGENTS.md` guidance.

- `ProjectARVRPro.csproj` `VersionPrefix` and `manifest.json` `version` are one version pair and must stay identical.
- The main application version in `Directory.Build.props` is separate. When a task releases both products, update and verify both version sources explicitly.
- Build `Projects/ProjectARVRPro/ProjectARVRPro.csproj` and run `Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj` for relevant changes.
