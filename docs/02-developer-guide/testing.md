# 测试与验证交接手册

本页把当前仓库里的测试入口按真实代码归类。接手代码时不要只记一个 `dotnet test`，因为当前测试分为 WPF/xUnit、native OpenCV helper、后端脚本和文档站构建几条链。

## 当前测试入口

| 测试区域 | 目录 | 技术栈 | 主要验证内容 | 运行入口 |
| --- | --- | --- | --- | --- |
| UI 与主程序逻辑测试 | `Test/ColorVision.UI.Tests/` | xUnit、`net10.0-windows`、WPF | UI 基础设施、Copilot/MCP、日志、Marketplace、PropertyGrid、终端缓冲、STNode、排序和编辑器辅助逻辑 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| native OpenCV helper 验证 | `Test/opencv_helper_test/` | Visual C++、OpenCV、x64 | `opencv_helper` 侧函数，例如 `M_FindLuminousArea` | Visual Studio 2022 或 `msbuild opencv_helper_test.vcxproj` |
| 插件市场后端测试 | `Web/Backend/` | Python/Flask | Marketplace API、release 记录、上传下载和存储行为 | `python test_app.py`、`python test_app_releases.py` |
| 构建脚本测试 | `Scripts/` | Python | 构建、打包、发布脚本的局部逻辑 | `pytest Scripts/test_*.py -v` |
| 文档站验证 | `docs/` | VitePress | 导航、Markdown、搜索索引、静态页面生成 | `npm run docs:build` |

## `ColorVision.UI.Tests`

这是当前最主要的 .NET 测试项目。工程文件声明 `TargetFramework=net10.0-windows`、`UseWPF=true`、`IsTestProject=true`，并引用主程序、`ColorVision.UI.Desktop`、`ColorVision.UI`、`ColorVision.Solution` 和 `ST.Library.UI`。

它的覆盖面已经不只是旧 README 里写的排序功能：

| 测试文件 | 覆盖面 |
| --- | --- |
| `ConfigServiceAdaptersTests.cs` | 配置服务 adapter 和基础配置行为 |
| `BrushJsonConverterTests.cs` | WPF brush JSON 序列化 |
| `PropertyEditorWindowTests.cs` | PropertyGrid/属性编辑窗口基础行为 |
| `ListEditorTests.cs`、`NestedListEditorTests.cs` | 列表编辑器和嵌套列表编辑 |
| `UniversalSortTests.cs`、`SortManagerTests.cs` | 通用排序和排序管理 |
| `TreemapLayoutTests.cs` | Treemap 布局计算 |
| `TerminalScreenBufferTests.cs` | 终端屏幕缓冲和滚动行为 |
| `STNodeCopyPasteTests.cs` | Flow/STNode 复制粘贴逻辑 |
| `LogEntryParserTests.cs`、`LogHistoryReaderTests.cs`、`LogSearchHelperTests.cs` | 日志解析、历史读取和搜索 |
| `MarketplacePackageDownloadServiceTests.cs` | 插件市场包下载、校验和临时目录处理 |
| `CopilotMcpTests.cs`、`CopilotCapabilitiesTests.cs`、`CopilotBusinessContextTests.cs`、`CopilotProfileConfigTests.cs`、`CopilotSearchDocsToolTests.cs`、`CopilotUiTextTests.cs` | Copilot/MCP 能力、业务上下文、配置、文档搜索和 UI 文案 |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64

dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcpTests"

dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

如果测试在非 Windows 环境失败，先确认是不是 WPF/Windows Desktop Runtime 限制。这个项目不是跨平台测试项目。

## native OpenCV helper 测试

`Test/opencv_helper_test/` 是 C++ 验证工程，不属于 xUnit。它用于验证 native `opencv_helper` 侧函数，当前有一份详细的 `Test/opencv_helper_test/BUILD_AND_DEBUG_GUIDE.md`。

```powershell
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64

Test/opencv_helper_test/build_test_find_luminous.bat
```

这条链主要服务 native 算法或 OpenCV helper 变更。只改 WPF 菜单、插件市场、配置窗口时，不需要默认跑它；改 `Native/`、`Engine/cvColorVision/`、`UI/ColorVision.Core/` 或 OpenCV DLL 输出时，应把它列入验收。

## 后端和脚本测试

插件市场后端：

```powershell
cd Web/Backend
python test_app.py
python test_app_releases.py
```

构建脚本：

```powershell
pytest Scripts/test_*.py -v
```

如果当前机器没有 Python 依赖，先按 [插件市场后端](./backend/README.md) 和 [构建与发布脚本](./scripts/README.md) 的依赖说明准备环境。不要把“依赖没装”误写成业务逻辑失败。

## 按变更选择验证

| 变更类型 | 至少验证 |
| --- | --- |
| UI 菜单、设置、PropertyGrid、列表编辑、日志或终端 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Copilot/MCP、文档搜索、业务上下文 | `Copilot*Tests`、`CopilotSearchDocsToolTests` |
| 插件市场下载、包校验、临时目录 | `MarketplacePackageDownloadServiceTests`，再看 [现有插件现场验收与交接清单](../04-api-reference/plugins/plugin-field-acceptance.md) |
| Flow 节点复制粘贴或 STNode 行为 | `STNodeCopyPasteTests`，再看 [模板与 Flow 链路](../04-api-reference/engine-components/template-flow-chain.md) |
| native/OpenCV helper | `opencv_helper_test`，并确认 runtime DLL 输出 |
| 插件市场后端 | `Web/Backend/test_app*.py` |
| 打包脚本 | `Scripts/test_*.py`，再跑目标 `package_plugin.bat` 或 `package_project.bat` |
| 文档站 | `npm run docs:build`，必要时访问本地路由 |

## 交接记录模板

| 字段 | 示例 |
| --- | --- |
| 变更范围 | UI/ColorVision.UI、Plugins/Spectrum、Projects/ProjectLUX |
| 测试命令 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| 运行环境 | Windows、x64、.NET SDK、Python/VS 版本 |
| 结果 | Passed / Failed，失败数量和首个失败类名 |
| 未跑原因 | 缺设备、缺 native DLL、缺 Python 依赖、只改文档 |
| 后续人工验收 | 主程序启动、插件菜单、项目流程、文档路由 |

## 维护规则

- 新增 `.csproj` 测试项目时，同步更新本页、[模块与文档对照表](../05-resources/project-structure/module-documentation-map.md) 和侧边栏导航。
- 新增关键测试类时，把它归入本页的测试文件覆盖表。
- 不要把 `Test/**/bin`、`Test/**/obj` 当成源码证据；它们只是历史构建输出。
- 修改 UI、Engine、插件或项目文档后，仍需运行 `npm run docs:build` 验证文档站。
