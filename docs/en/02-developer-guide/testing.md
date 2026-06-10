# Testing And Validation Handoff

This page maps the current test entry points in the repository. Do not reduce testing to one `dotnet test` command: the repo currently has WPF/xUnit tests, native OpenCV helper checks, backend script tests, and documentation-site validation.

## Current Test Entry Points

| Area | Directory | Stack | What it verifies | Entry |
| --- | --- | --- | --- | --- |
| UI and host logic | `Test/ColorVision.UI.Tests/` | xUnit, `net10.0-windows`, WPF | UI infrastructure, Copilot/MCP, logs, marketplace, PropertyGrid, terminal buffer, STNode, sorting, editor helpers | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Native OpenCV helper | `Test/opencv_helper_test/` | Visual C++, OpenCV, x64 | `opencv_helper` functions such as `M_FindLuminousArea` | Visual Studio 2022 or `msbuild opencv_helper_test.vcxproj` |
| Marketplace backend | `Backend/marketplace/` | Python/Flask | Marketplace API, release records, upload/download, storage behavior | `python test_app.py`, `python test_app_releases.py` |
| Build scripts | `Scripts/` | Python | Build, package, publish helper logic | `pytest Scripts/test_*.py -v` |
| Documentation site | `docs/` | VitePress | Navigation, Markdown, search index, static pages | `npm run docs:build` |

## `ColorVision.UI.Tests`

This is the main .NET test project. The project file declares `net10.0-windows`, `UseWPF=true`, `IsTestProject=true`, and references the host, UI Desktop, UI, Solution, and `ST.Library.UI`.

| Test file | Coverage |
| --- | --- |
| `ConfigServiceAdaptersTests.cs` | configuration service adapters |
| `BrushJsonConverterTests.cs` | WPF brush JSON serialization |
| `PropertyEditorWindowTests.cs` | PropertyGrid/editor window behavior |
| `ListEditorTests.cs`, `NestedListEditorTests.cs` | list editors |
| `UniversalSortTests.cs`, `SortManagerTests.cs` | sorting |
| `TreemapLayoutTests.cs` | treemap layout |
| `TerminalScreenBufferTests.cs` | terminal screen buffer |
| `STNodeCopyPasteTests.cs` | Flow/STNode copy and paste |
| `LogEntryParserTests.cs`, `LogHistoryReaderTests.cs`, `LogSearchHelperTests.cs` | log parsing, history, search |
| `MarketplacePackageDownloadServiceTests.cs` | package download and temporary file handling |
| `CopilotMcpTests.cs`, `CopilotCapabilitiesTests.cs`, `CopilotBusinessContextTests.cs`, `CopilotProfileConfigTests.cs`, `CopilotSearchDocsToolTests.cs`, `CopilotUiTextTests.cs` | Copilot/MCP capabilities, business context, profiles, docs search, UI text |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcpTests"
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

This project is Windows-only because it uses WPF and Windows Desktop runtime.

## Native OpenCV Helper

`Test/opencv_helper_test/` is a C++ verification project, not xUnit. Use it when changing `Native/`, `Engine/cvColorVision/`, `UI/ColorVision.Core/`, OpenCV helper code, or native runtime output.

```powershell
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

The detailed native guide lives in `Test/opencv_helper_test/BUILD_AND_DEBUG_GUIDE.md`.

## Backend And Script Tests

```powershell
cd Backend/marketplace
python test_app.py
python test_app_releases.py

pytest Scripts/test_*.py -v
```

If dependencies are missing, prepare the environment from [Plugin Marketplace Backend](./backend/README.md) and [Build and Release Scripts](./scripts/README.md). Do not record missing dependencies as business failures.

## Choose Validation By Change

| Change | Minimum validation |
| --- | --- |
| UI menus, settings, PropertyGrid, list editor, logs, terminal | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Copilot/MCP, docs search, business context | `Copilot*Tests`, `CopilotSearchDocsToolTests` |
| Marketplace download or package checks | `MarketplacePackageDownloadServiceTests` and plugin field acceptance |
| Flow/STNode copy paste | `STNodeCopyPasteTests` and [Templates And Flow Chain](../04-api-reference/engine-components/template-flow-chain.md) |
| native/OpenCV helper | `opencv_helper_test` and runtime DLL output |
| marketplace backend | `Backend/marketplace/test_app*.py` |
| packaging scripts | `Scripts/test_*.py` and the target package script |
| docs site | `npm run docs:build` and route checks when needed |

## Maintenance Rules

- Add new test projects to this page, the module-documentation map, and sidebar navigation.
- Add important new test classes to the coverage table.
- Do not treat `Test/**/bin` or `Test/**/obj` as source evidence.
- After documentation changes, still run `npm run docs:build`.
