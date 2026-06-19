# テストと検証の引き継ぎ

このページは、現在のリポジトリにあるテスト入口を実コードに合わせて整理します。テストは `dotnet test` だけではありません。WPF/xUnit、native OpenCV helper、backend script、docs build のチェーンがあります。

## 現在のテスト入口

| 領域 | ディレクトリ | 技術 | 検証内容 | 入口 |
| --- | --- | --- | --- | --- |
| UI と host logic | `Test/ColorVision.UI.Tests/` | xUnit、`net10.0-windows`、WPF | UI infrastructure、Copilot/MCP、log、marketplace、PropertyGrid、terminal buffer、STNode、sorting | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| native OpenCV helper | `Test/opencv_helper_test/` | Visual C++、OpenCV、x64 | `opencv_helper` functions、例: `M_FindLuminousArea` | Visual Studio 2022 または `msbuild opencv_helper_test.vcxproj` |
| marketplace backend | `Web/Backend/` | Python/Flask | Marketplace API、release record、upload/download、storage | `python test_app.py`、`python test_app_releases.py` |
| build scripts | `Scripts/` | Python | build/package/publish helper logic | `pytest Scripts/test_*.py -v` |
| docs site | `docs/` | VitePress | navigation、Markdown、search index、static pages | `npm run docs:build` |

## `ColorVision.UI.Tests`

現在の主要な .NET test project です。project file は `net10.0-windows`、`UseWPF=true`、`IsTestProject=true` を宣言し、host、UI Desktop、UI、Solution、`ST.Library.UI` を参照します。

| テストファイル | カバー範囲 |
| --- | --- |
| `ConfigServiceAdaptersTests.cs` | configuration service adapters |
| `BrushJsonConverterTests.cs` | WPF brush JSON serialization |
| `PropertyEditorWindowTests.cs` | PropertyGrid/editor window behavior |
| `ListEditorTests.cs`、`NestedListEditorTests.cs` | list editors |
| `UniversalSortTests.cs`、`SortManagerTests.cs` | sorting |
| `TreemapLayoutTests.cs` | treemap layout |
| `TerminalScreenBufferTests.cs` | terminal screen buffer |
| `STNodeCopyPasteTests.cs` | Flow/STNode copy paste |
| `LogEntryParserTests.cs`、`LogHistoryReaderTests.cs`、`LogSearchHelperTests.cs` | log parsing、history、search |
| `MarketplacePackageDownloadServiceTests.cs` | package download and temporary file handling |
| `CopilotMcpTests.cs`、`CopilotCapabilitiesTests.cs`、`CopilotBusinessContextTests.cs`、`CopilotProfileConfigTests.cs`、`CopilotSearchDocsToolTests.cs`、`CopilotUiTextTests.cs` | Copilot/MCP capabilities、business context、profiles、docs search、UI text |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcpTests"
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

WPF と Windows Desktop Runtime を使うため、この project は Windows only です。

## native OpenCV helper

`Test/opencv_helper_test/` は C++ verification project で、xUnit ではありません。`Native/`、`Engine/cvColorVision/`、`UI/ColorVision.Core/`、OpenCV helper、native runtime output を変更した場合に確認します。

```powershell
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

詳細は `Test/opencv_helper_test/BUILD_AND_DEBUG_GUIDE.md` です。

## backend と scripts

```powershell
cd Web/Backend
python test_app.py
python test_app_releases.py

pytest Scripts/test_*.py -v
```

依存関係がない場合は [Plugin Marketplace Backend](./backend/README.md) と [Build and Release Scripts](./scripts/README.md) から環境を準備します。dependency missing を business failure として記録しないでください。

## 変更別の最小検証

| 変更 | 最小検証 |
| --- | --- |
| UI menu、settings、PropertyGrid、list editor、logs、terminal | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Copilot/MCP、docs search、business context | `Copilot*Tests`、`CopilotSearchDocsToolTests` |
| marketplace download/package check | `MarketplacePackageDownloadServiceTests` と plugin field acceptance |
| Flow/STNode copy paste | `STNodeCopyPasteTests` と [Templates and Flow Chain](../04-api-reference/engine-components/template-flow-chain.md) |
| native/OpenCV helper | `opencv_helper_test` と runtime DLL output |
| marketplace backend | `Web/Backend/test_app*.py` |
| packaging scripts | `Scripts/test_*.py` と対象 package script |
| docs site | `npm run docs:build` と必要な route check |

## 保守ルール

- 新しい test project を追加したら、このページ、module-documentation map、sidebar を更新します。
- 重要な test class を追加したら coverage table に入れます。
- `Test/**/bin` と `Test/**/obj` は source evidence として扱いません。
- docs を変更したら `npm run docs:build` を実行します。
