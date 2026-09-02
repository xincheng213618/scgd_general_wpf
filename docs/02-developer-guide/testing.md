---
knowledge_id: "delivery.testing"
knowledge_type: "reference"
status: "current"
summary: "按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。"
aliases: ["怎么测试","验证命令","dotnet test","测试入口","PerformanceProbe","COLORVISION_IMAGE_ALGORITHM_PERF"]
code_paths: ["Test","Scripts/tests","Web/Backend","package.json",".github/workflows/dotnet.yml"]
test_paths: ["Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj","Test/ColorVision.Copilot.Tests/ColorVision.Copilot.Tests.csproj","Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs","Test/ColorVision.Copilot.Tests/CopilotConfigurationIsolationTests.cs"]
related: ["delivery.index","delivery.prerequisites","delivery.native-testing","governance.retrieval","copilot.configuration"]
---

# 测试与验证

本页把当前仓库里的测试入口按真实代码归类。维护代码时不要只记一个 `dotnet test`，因为当前测试分为 WPF/xUnit、native OpenCV helper、后端和文档站构建几条链。

## 当前测试入口

| 测试区域 | 目录 | 技术栈 | 主要验证内容 | 运行入口 |
| --- | --- | --- | --- | --- |
| Copilot 与 Agent 测试 | `Test/ColorVision.Copilot.Tests/` | xUnit、`net10.0-windows`、WPF | Copilot、ColorVision 配置与外部 TOML 隔离、Agent、MCP、审批、Hook、Skill、会话恢复与工作区安全边界 | `dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64` |
| UI 与主程序逻辑测试 | `Test/ColorVision.UI.Tests/` | xUnit、`net10.0-windows`、WPF | UI 基础设施、日志、Marketplace、PropertyGrid、终端缓冲、STNode、排序和编辑器辅助逻辑 | `dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64` |
| Spectrum、Conoscope 与客户项目测试 | `Test/Spectrum.Tests/`、`Test/Conoscope.Tests/`、`Test/ProjectARVRPro.Tests/`、`Test/ProjectKB.Tests/`、`Test/ProjectLUX.Tests/` | xUnit、`net10.0-windows`、WPF | 光谱、Conoscope 和三个客户项目的可脱离设备运行的领域回归 | 分别对目标 `.csproj` 执行 `dotnet test -c Release -p:Platform=x64` |
| 构建、发布和打包脚本测试 | `Scripts/tests/` | Python `unittest` | ABI、平台、安装器、更新包、插件包、后端客户端和发布编排的静态及合成制品门禁 | `python -m unittest discover -s Scripts/tests -p "test_*.py" -v` |
| native OpenCV helper 验证 | `Test/opencv_helper_test/` | Visual C++、OpenCV、x64 | 图像缓冲、检测、校准/POI、SFR、P2、视频和日志等原生回归 | [原生测试与调试](./engine-development/native-testing.md)：按项目工具集、配置和专项参数运行 |
| 插件市场后端测试 | `Web/Backend/` | Python/Flask | Marketplace API、release 记录、上传下载和存储行为 | `python test_app.py`、`python test_app_releases.py` |
| 知识与文档验证 | `docs/` | Node、VitePress | 元数据、源码/测试引用、检索路由、导航、网页搜索 | `npm run docs:check`、`npm run docs:build` |

## `ColorVision.UI.Tests`

这是普通 UI 与主程序基础设施测试项目。工程声明 `TargetFramework=net10.0-windows`、`UseWPF=true`、`IsTestProject=true`；Copilot 测试不再由这个程序集承载。

| 测试文件 | 覆盖面 |
| --- | --- |
| `ConfigServiceAdaptersTests.cs`、`ConfigHandlerPersistenceTests.cs`、`ThemeSettingsTests.cs` | 配置 adapter、配置持久化和主题设置 |
| `PropertyEditorContractTests.cs`、`PropertyEditSessionTests.cs`、`ListEditorTests.cs` | PropertyGrid 契约、编辑会话和列表编辑器 |
| `FindCrossResultOverlayTests.cs`、`AlgorithmResultOverlayTests.cs`、`AlgorithmOverlayManagerTests.cs` | 历史结果坐标、算法叠加内容、临时/持久Overlay生命周期；三者不是同一个职责 |
| `ResultImagePresentationTests.cs` 与 `Test/ProjectARVRPro.Tests/ResultImagePresentationTests.cs` | 不同宿主/项目的图像呈现规则，按主题核对实际目标 |
| `UniversalSortTests.cs`、`TreemapLayoutTests.cs` | 通用排序、Treemap 布局 |
| `TerminalScreenBufferTests.cs`、`STNodeCopyPasteTests.cs` | 终端屏幕缓冲、Flow/STNode 复制粘贴 |
| `LogEntryParserTests.cs`、`LogHistoryReaderTests.cs`、`LogSearchHelperTests.cs` | 日志解析、历史读取和搜索 |
| `MarketplacePackageDownloadServiceTests.cs` | 插件市场包下载、校验和临时目录处理 |

```powershell
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~UniversalSortTests"
dotnet test Test/ColorVision.UI.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~MarketplacePackageDownloadServiceTests"
```

如果测试在非 Windows 环境失败，先确认是不是 WPF/Windows Desktop Runtime 限制。这个项目不是跨平台测试项目。UI 与 Copilot 虽分成两个测试程序集，但都引用宿主与共享模块，不是无宿主依赖的纯单元测试包；首次构建仍受[环境与 native 前提](../00-getting-started/prerequisites.md)约束。

### 普通回归与性能探针

当前 `.github/workflows/dotnet.yml` 将 UI 普通回归与 `Category=PerformanceProbe` 分两次 `dotnet test` 启动，后者独立进程运行。无筛选的命令仍有效，但会把两个分类放进同一次运行，不能称为 CI 普通回归那一步的等价命令。只复现分类选择时，从仓库根目录运行：

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64 --filter "Category!=PerformanceProbe"
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64 --filter "Category=PerformanceProbe"
```

这些命令会构建/运行测试并写入本地产物，不等同于纯文档校验。`ImageAlgorithmPerformanceGateTests` 中调用 `Enabled()` 的 4K/8K 探针，仅在 `COLORVISION_IMAGE_ALGORITHM_PERF=1` 时执行测量，否则记录说明后直接返回；不是这个分类里的所有测试都受同一个开关控制。测试整体通过不能证明所有大型性能测量已经执行，记录时需核对筛选、环境变量与实际输出。

两个测试项目的 `AssemblyInfo.cs` 都禁用测试集合并行，原因是进程级注册器、状态和 WPF 服务共享。`UseWPF=true` 不会让所有测试线程自动成为 STA；两个项目通过源码链接共用 `Test/Shared/StaTest.cs`：只需独立 STA 的同步操作使用 `StaTest.Run`，有超时要求的测试保留各自时限和失败提示；需要共享 `Application` 和消息循环的操作使用 `WpfTestHost`。两者的线程生命周期不同，不为提速取消这些边界。

## `ColorVision.Copilot.Tests`

这是 Copilot 模块的独立测试程序集。现有 `Copilot*.cs`、桌面宠物 Copilot 状态映射，以及批量图片 Copilot 工具边界都由本项目承载；新增 Copilot、Agent 或 MCP 回归不要再放回普通 UI 测试项目。

```powershell
dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64
dotnet test Test/ColorVision.Copilot.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcp"
```

Copilot 的配置契约见[配置与指令来源](./core-concepts/copilot-configuration.md)：模型、供应商、工具与审批由 ColorVision 管理，不加载全局或项目 `config.toml`。`CopilotConfigurationIsolationTests` 验证外部 TOML 不覆盖应用设置且项目指令仍可发现；这是当前负向隔离覆盖，不应当作过期加载测试删除。

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
| native OpenCV helper | 按[原生测试指南](./engine-development/native-testing.md)构建 `.vcxproj`，选择实际覆盖改动的专项；核对退出码和跳过信息 | 改 helper、其托管调用或 OpenCV DLL 输出；供应商 `cvColorVision` 另按该模块的真实入口验收 |
| 后端 | 在 `Web/Backend` 中运行目标 unittest 模块；需要全量 discover 时用下方命令 | 改插件市场 API、release、上传下载或存储 |

后端测试从 Backend 目录发现模块；先按[后端测试边界](./backend/README.md#测试与边界)核对 Python 依赖、配置及每个测试的数据隔离。下例执行测试并可能生成本地产物：

```powershell
Push-Location .\Web\Backend
try {
    python -m unittest discover -p "test_*.py"
    if ($LASTEXITCODE -ne 0) { throw '后端测试失败，检查首个失败及测试输出。' }
}
finally {
    Pop-Location
}
```

如果当前机器没有 Python 依赖，先按[插件市场后端](./backend/README.md)和[构建与发布脚本](./scripts/README.md)准备环境。不要把“依赖没装”误写成业务逻辑失败。

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
| 打包脚本 | 先跑 `Scripts/tests` 的相关合成制品测试与清单校验；wrapper会上传，仅在已授权发布时执行远端验收 |
| 知识与文档 | `npm run docs:knowledge`、`npm run docs:check`；网页内容/导航改变时再 `npm run docs:build` |

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

- 新增测试项目或关键测试类时，同步对应知识的 `test_paths` 和必要的验证说明，再生成目录；侧边栏自动派生，不手工维护。不要把 `Test/**/bin`、`Test/**/obj` 当成源码证据。
- 修改 UI、Engine、插件或项目文档后，仍需运行 `npm run docs:build` 验证文档站。
- 快速发布遵守根 `AGENTS.md` 的专用入口与范围，不因为本页列了测试就额外扩大发布流程。
- 元数据/链接校验只证明引用和路由有效；模型是否理解正确仍需[冷启动问答抽样](../knowledge/retrieval-checks.md)，真实设备与正式交付各自验收。
