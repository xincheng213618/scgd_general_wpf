# 測試與驗證交接手冊

本頁把目前倉庫裡的測試入口按真實程式碼歸類。不要只記一個 `dotnet test`，因為目前測試分成 WPF/xUnit、native OpenCV helper、後端腳本和文件站建置幾條鏈。

## 目前測試入口

| 測試區域 | 目錄 | 技術棧 | 主要驗證內容 | 執行入口 |
| --- | --- | --- | --- | --- |
| UI 與主程式邏輯測試 | `Test/ColorVision.UI.Tests/` | xUnit、`net10.0-windows`、WPF | UI 基礎設施、Copilot/MCP、日誌、Marketplace、PropertyGrid、終端緩衝、STNode、排序和編輯器輔助邏輯 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| native OpenCV helper 驗證 | `Test/opencv_helper_test/` | Visual C++、OpenCV、x64 | `opencv_helper` 側函式，例如 `M_FindLuminousArea` | Visual Studio 2022 或 `msbuild opencv_helper_test.vcxproj` |
| 外掛市場後端測試 | `Web/Backend/` | Python/Flask | Marketplace API、release 記錄、上傳下載和儲存行為 | `python test_app.py`、`python test_app_releases.py` |
| 建置腳本測試 | `Scripts/` | Python | 建置、打包、發布腳本局部邏輯 | `pytest Scripts/test_*.py -v` |
| 文件站驗證 | `docs/` | VitePress | 導航、Markdown、搜尋索引、靜態頁生成 | `npm run docs:build` |

## `ColorVision.UI.Tests`

這是目前最主要的 .NET 測試專案。工程文件宣告 `net10.0-windows`、`UseWPF=true`、`IsTestProject=true`，並引用主程式、UI Desktop、UI、Solution 和 `ST.Library.UI`。

目前測試覆蓋面已不只是排序功能：

| 測試文件 | 覆蓋面 |
| --- | --- |
| `ConfigServiceAdaptersTests.cs` | 配置服務 adapter |
| `BrushJsonConverterTests.cs` | WPF brush JSON 序列化 |
| `PropertyEditorWindowTests.cs` | PropertyGrid/屬性編輯窗口 |
| `ListEditorTests.cs`、`NestedListEditorTests.cs` | 列表編輯器 |
| `UniversalSortTests.cs`、`SortManagerTests.cs` | 通用排序和排序管理 |
| `TreemapLayoutTests.cs` | Treemap 佈局 |
| `TerminalScreenBufferTests.cs` | 終端畫面緩衝 |
| `STNodeCopyPasteTests.cs` | Flow/STNode 複製貼上 |
| `LogEntryParserTests.cs`、`LogHistoryReaderTests.cs`、`LogSearchHelperTests.cs` | 日誌解析、歷史讀取和搜尋 |
| `MarketplacePackageDownloadServiceTests.cs` | 外掛市場包下載與暫存檔處理 |
| `CopilotMcpTests.cs`、`CopilotCapabilitiesTests.cs`、`CopilotBusinessContextTests.cs`、`CopilotProfileConfigTests.cs`、`CopilotSearchDocsToolTests.cs`、`CopilotUiTextTests.cs` | Copilot/MCP 能力、業務上下文、設定、文件搜尋和 UI 文案 |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcpTests"
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

這個專案使用 WPF 和 Windows Desktop Runtime，因此不是跨平台測試。

## native OpenCV helper

`Test/opencv_helper_test/` 是 C++ 驗證工程，不屬於 xUnit。修改 `Native/`、`Engine/cvColorVision/`、`UI/ColorVision.Core/`、OpenCV helper 或 native runtime 輸出時，應把它列入驗收。

```powershell
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

詳細指南在 `Test/opencv_helper_test/BUILD_AND_DEBUG_GUIDE.md`。

## 後端和腳本測試

```powershell
cd Web/Backend
python test_app.py
python test_app_releases.py

pytest Scripts/test_*.py -v
```

如果缺 Python 依賴，先按 [外掛市場後端](./backend/README.md) 和 [建置與發布腳本](./scripts/README.md) 準備環境，不要把依賴未安裝記成業務失敗。

## 按變更選擇驗證

| 變更類型 | 至少驗證 |
| --- | --- |
| UI 選單、設定、PropertyGrid、列表、日誌或終端 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Copilot/MCP、文件搜尋、業務上下文 | `Copilot*Tests`、`CopilotSearchDocsToolTests` |
| Marketplace 下載或包校驗 | `MarketplacePackageDownloadServiceTests` 和外掛現場驗收 |
| Flow/STNode 複製貼上 | `STNodeCopyPasteTests` 和 [模板與 Flow 鏈路](../04-api-reference/engine-components/template-flow-chain.md) |
| native/OpenCV helper | `opencv_helper_test` 和 runtime DLL 輸出 |
| 外掛市場後端 | `Web/Backend/test_app*.py` |
| 打包腳本 | `Scripts/test_*.py` 和目標打包腳本 |
| 文件站 | `npm run docs:build`，必要時檢查路由 |

## 維護規則

- 新增測試專案時，同步更新本頁、模組文件對照表和側邊欄。
- 新增關鍵測試類時，把它加入覆蓋表。
- 不要把 `Test/**/bin` 或 `Test/**/obj` 當成源碼證據。
- 修改文件後仍要執行 `npm run docs:build`。
