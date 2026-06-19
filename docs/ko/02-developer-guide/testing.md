# 테스트 및 검증 인수인계

이 페이지는 현재 저장소의 테스트 진입점을 실제 코드 기준으로 정리합니다. 테스트는 `dotnet test` 하나로 끝나지 않습니다. WPF/xUnit, native OpenCV helper, backend script, docs build 체인이 따로 있습니다.

## 현재 테스트 진입점

| 영역 | 디렉터리 | 기술 | 검증 내용 | 진입점 |
| --- | --- | --- | --- | --- |
| UI 및 host logic | `Test/ColorVision.UI.Tests/` | xUnit, `net10.0-windows`, WPF | UI infrastructure, Copilot/MCP, log, marketplace, PropertyGrid, terminal buffer, STNode, sorting | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| native OpenCV helper | `Test/opencv_helper_test/` | Visual C++, OpenCV, x64 | `opencv_helper` functions, 예: `M_FindLuminousArea` | Visual Studio 2022 또는 `msbuild opencv_helper_test.vcxproj` |
| marketplace backend | `Backend/marketplace/` | Python/Flask | Marketplace API, release record, upload/download, storage | `python test_app.py`, `python test_app_releases.py` |
| build scripts | `Scripts/` | Python | build/package/publish helper logic | `pytest Scripts/test_*.py -v` |
| docs site | `docs/` | VitePress | navigation, Markdown, search index, static pages | `npm run docs:build` |

## `ColorVision.UI.Tests`

현재 주요 .NET test project입니다. project file은 `net10.0-windows`, `UseWPF=true`, `IsTestProject=true`를 선언하고 host, UI Desktop, UI, Solution, `ST.Library.UI`를 참조합니다.

| 테스트 파일 | 커버 범위 |
| --- | --- |
| `ConfigServiceAdaptersTests.cs` | configuration service adapters |
| `BrushJsonConverterTests.cs` | WPF brush JSON serialization |
| `PropertyEditorWindowTests.cs` | PropertyGrid/editor window behavior |
| `ListEditorTests.cs`, `NestedListEditorTests.cs` | list editors |
| `UniversalSortTests.cs`, `SortManagerTests.cs` | sorting |
| `TreemapLayoutTests.cs` | treemap layout |
| `TerminalScreenBufferTests.cs` | terminal screen buffer |
| `STNodeCopyPasteTests.cs` | Flow/STNode copy paste |
| `LogEntryParserTests.cs`, `LogHistoryReaderTests.cs`, `LogSearchHelperTests.cs` | log parsing, history, search |
| `MarketplacePackageDownloadServiceTests.cs` | package download and temporary file handling |
| `CopilotMcpTests.cs`, `CopilotCapabilitiesTests.cs`, `CopilotBusinessContextTests.cs`, `CopilotProfileConfigTests.cs`, `CopilotSearchDocsToolTests.cs`, `CopilotUiTextTests.cs` | Copilot/MCP capabilities, business context, profiles, docs search, UI text |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcpTests"
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

WPF와 Windows Desktop Runtime을 사용하므로 이 project는 Windows only입니다.

## native OpenCV helper

`Test/opencv_helper_test/`는 C++ verification project이며 xUnit이 아닙니다. `Native/`, `Engine/cvColorVision/`, `UI/ColorVision.Core/`, OpenCV helper, native runtime output을 변경할 때 확인합니다.

```powershell
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

자세한 내용은 `Test/opencv_helper_test/BUILD_AND_DEBUG_GUIDE.md`입니다.

## backend 및 scripts

```powershell
cd Backend/marketplace
python test_app.py
python test_app_releases.py

pytest Scripts/test_*.py -v
```

의존성이 없으면 [Plugin Marketplace Backend](./backend/README.md)와 [Build and Release Scripts](./scripts/README.md)에서 환경을 준비합니다. dependency missing을 business failure로 기록하지 마세요.

## 변경별 최소 검증

| 변경 | 최소 검증 |
| --- | --- |
| UI menu, settings, PropertyGrid, list editor, logs, terminal | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Copilot/MCP, docs search, business context | `Copilot*Tests`, `CopilotSearchDocsToolTests` |
| marketplace download/package check | `MarketplacePackageDownloadServiceTests` 및 plugin field acceptance |
| Flow/STNode copy paste | `STNodeCopyPasteTests` 및 [Templates and Flow Chain](../04-api-reference/engine-components/template-flow-chain.md) |
| native/OpenCV helper | `opencv_helper_test` 및 runtime DLL output |
| marketplace backend | `Backend/marketplace/test_app*.py` |
| packaging scripts | `Scripts/test_*.py` 및 대상 package script |
| docs site | `npm run docs:build` 및 필요한 route check |

## 유지보수 규칙

- 새 test project를 추가하면 이 페이지, module-documentation map, sidebar를 업데이트합니다.
- 중요한 test class를 추가하면 coverage table에 넣습니다.
- `Test/**/bin`과 `Test/**/obj`를 source evidence로 취급하지 않습니다.
- docs를 변경하면 `npm run docs:build`를 실행합니다.
