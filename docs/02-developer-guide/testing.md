# 测试与验证

本页把当前仓库里的测试入口按真实代码归类。维护代码时不要只记一个 `dotnet test`，因为当前测试分为 WPF/xUnit、native OpenCV helper、后端和文档站构建几条链。

## 当前测试入口

| 测试区域 | 目录 | 技术栈 | 主要验证内容 | 运行入口 |
| --- | --- | --- | --- | --- |
| Copilot 与 Agent 测试 | `Test/ColorVision.Copilot.Tests/` | xUnit、`net10.0-windows`、WPF | Copilot、Codex 配置、Agent、MCP、审批、Hook、Skill、会话恢复与工作区安全边界 | `dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64` |
| UI 与主程序逻辑测试 | `Test/ColorVision.UI.Tests/` | xUnit、`net10.0-windows`、WPF | UI 基础设施、日志、Marketplace、PropertyGrid、终端缓冲、STNode、排序和编辑器辅助逻辑 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Spectrum、Conoscope 与客户项目测试 | `Test/Spectrum.Tests/`、`Test/Conoscope.Tests/`、`Test/ProjectARVRPro.Tests/`、`Test/ProjectKB.Tests/`、`Test/ProjectLUX.Tests/` | xUnit、`net10.0-windows`、WPF | 光谱、Conoscope 和三个客户项目的可脱离设备运行的领域回归 | 分别对目标 `.csproj` 执行 `dotnet test -c Release -p:Platform=x64` |
| 构建、发布和打包脚本测试 | `Scripts/tests/` | Python `unittest` | ABI、平台、安装器、更新包、插件包、后端客户端和发布编排的静态及合成制品门禁 | `python -m unittest discover -s Scripts/tests -p "test_*.py" -v` |
| native OpenCV helper 验证 | `Test/opencv_helper_test/` | Visual C++、OpenCV、x64 | `opencv_helper` 侧函数，例如 `M_FindLuminousArea` | Visual Studio 2022 或 `msbuild opencv_helper_test.vcxproj` |
| 插件市场后端测试 | `Web/Backend/` | Python/Flask | Marketplace API、release 记录、上传下载和存储行为 | `python test_app.py`、`python test_app_releases.py` |
| 文档站验证 | `docs/` | VitePress | 导航、Markdown、搜索索引、静态页面生成 | `npm run docs:build` |

## `ColorVision.UI.Tests`

这是普通 UI 与主程序基础设施测试项目。工程声明 `TargetFramework=net10.0-windows`、`UseWPF=true`、`IsTestProject=true`；Copilot 测试不再由这个程序集承载。

| 测试文件 | 覆盖面 |
| --- | --- |
| `ConfigServiceAdaptersTests.cs`、`ConfigHandlerPersistenceTests.cs`、`ThemeSettingsTests.cs` | 配置 adapter、配置持久化和主题设置 |
| `PropertyEditorContractTests.cs`、`PropertyEditSessionTests.cs`、`ListEditorTests.cs` | PropertyGrid 契约、编辑会话和列表编辑器 |
| `UniversalSortTests.cs`、`TreemapLayoutTests.cs` | 通用排序、Treemap 布局 |
| `TerminalScreenBufferTests.cs`、`STNodeCopyPasteTests.cs` | 终端屏幕缓冲、Flow/STNode 复制粘贴 |
| `LogEntryParserTests.cs`、`LogHistoryReaderTests.cs`、`LogSearchHelperTests.cs` | 日志解析、历史读取和搜索 |
| `MarketplacePackageDownloadServiceTests.cs` | 插件市场包下载、校验和临时目录处理 |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~UniversalSortTests"
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

如果测试在非 Windows 环境失败，先确认是不是 WPF/Windows Desktop Runtime 限制。这个项目不是跨平台测试项目。

## `ColorVision.Copilot.Tests`

这是 Copilot 模块的独立测试程序集。现有 `Copilot*.cs`、桌面宠物 Copilot 状态映射，以及批量图片 Copilot 工具边界都由本项目承载；新增 Copilot、Agent 或 MCP 回归不要再放回普通 UI 测试项目。

```powershell
dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64
dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcp"
```

## Spectrum 与 Conoscope

```powershell
dotnet test .\Test\Spectrum.Tests\Spectrum.Tests.csproj -p:Platform=x64
dotnet test .\Test\Conoscope.Tests\Conoscope.Tests.csproj -p:Platform=x64
```

- Spectrum 覆盖有效范围、标定预检/SHA-256 快照、CSV 实际波长与调用时快照、校正算法，以及 Manager 不得持有 UI 对象的架构边界；真机仍需验证光谱仪、快门、滤光轮和 SMU。
- Conoscope 覆盖 CVCIE 逐通道读取、Document staged load/所有权、ViewState/导出 readiness、关注点、分析、伪彩和 MVS 边界；设置 `CONOSCOPE_REAL_SAMPLE` 可追加真实样本测试，但不要把绝对路径写入仓库。

## native 和后端

| 链路 | 命令 | 什么时候跑 |
| --- | --- | --- |
| native OpenCV helper | `msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64`、`Test/opencv_helper_test/build_test_find_luminous.bat` | 改 `Native/`、`Engine/cvColorVision/`、`UI/ColorVision.Core/` 或 OpenCV DLL 输出 |
| 后端 | `cd Web/Backend` 后运行 `python test_app.py`、`python test_app_releases.py` | 改插件市场 API、release、上传下载或存储 |

如果当前机器没有 Python 依赖，先按 [插件市场后端](./backend/README.md) 和 [构建与发布脚本](./scripts/README.md) 准备环境。不要把“依赖没装”误写成业务逻辑失败。

仓库的 Windows `.NET` 工作流会运行两套公共 managed 测试、上表五套领域测试以及 `Scripts/tests` 的完整 discover。它仍不是“仓库全部测试”：需要 CUDA/OpenCV 或真实设备的 native 验证、插件市场后端测试、文档构建和现场硬件验收继续使用各自入口。

## 按变更选择验证

| 变更类型 | 至少验证 |
| --- | --- |
| UI 菜单、设置、PropertyGrid、列表编辑、日志或终端 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Copilot/MCP、文档搜索、业务上下文 | `dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64` |
| 插件市场下载、包校验、临时目录 | `MarketplacePackageDownloadServiceTests`，再看 [现有插件能力](../04-api-reference/plugins/README.md) |
| Flow 节点复制粘贴或 STNode 行为 | `STNodeCopyPasteTests`，再看 [模板与 Flow 链路](../04-api-reference/engine-components/template-flow-chain.md) |
| native/OpenCV helper | `opencv_helper_test`，并确认 runtime DLL 输出 |
| 插件市场后端 | `Web/Backend/test_app*.py` |
| 打包脚本 | 实际运行目标 `package_plugin.bat` 或 `package_project.bat` 并检查制品与上传结果 |
| 文档站 | `npm run docs:build`，必要时访问本地路由 |

## 验证记录

| 字段 | 示例 |
| --- | --- |
| 变更范围 | UI/ColorVision.UI、Plugins/Spectrum、Projects/ProjectLUX |
| 测试命令 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| 运行环境 | Windows、x64、.NET SDK、Python/VS 版本 |
| 结果 | Passed / Failed，失败数量和首个失败类名 |
| 未跑原因 | 缺设备、缺 native DLL、缺 Python 依赖、只改文档 |
| 后续人工验收 | 主程序启动、插件菜单、项目流程、文档路由 |

## 维护规则

- 新增测试项目或关键测试类时，同步本页、[项目结构总览](../05-resources/project-structure/README.md) 和侧边栏导航；不要把 `Test/**/bin`、`Test/**/obj` 当成源码证据。
- 修改 UI、Engine、插件或项目文档后，仍需运行 `npm run docs:build` 验证文档站。
