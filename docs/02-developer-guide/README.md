# 开发手册

本章节回答“怎么改代码、怎么构建、怎么测试、怎么交付”。根目录 `README.md` 是仓库第一入口；这里保留开发专题和更细的模块入口。

## 按任务进入

| 任务 | 入口 |
| --- | --- |
| 选择构建、测试和验收命令 | [测试与验证](./testing.md) |
| 构建安装包、更新包或发布包 | [部署概览](./deployment/overview.md)、[构建与发布脚本](./scripts/README.md) |
| 新增或维护插件 | [插件开发](./plugin-development/README.md)、[现有插件能力](../04-api-reference/plugins/README.md) |
| 维护客户项目包 | [项目说明](../00-projects/README.md)、[项目包总览](../04-api-reference/projects/README.md) |
| 修改 Engine、设备、模板或 Flow | [Engine 开发](./engine-development/README.md)、[Engine 组件](../04-api-reference/engine-components/README.md) |
| 修改 UI 类库、菜单、设置或图像编辑器 | [UI 组件](../04-api-reference/ui-components/README.md) |
| 新增 Flow 节点或扩展点 | [扩展点](../04-api-reference/extensions/README.md)、[Flow 节点扩展](../04-api-reference/extensions/flow-node.md) |
| 维护插件市场后端 | [插件市场后端](./backend/README.md) |
| 维护 Copilot Agent 或工具执行链 | [Copilot Agent Runtime](./core-concepts/copilot-agent-runtime.md) |
| 维护 Copilot 对话 UI 或状态 | [Copilot ViewModel 维护地图](./core-concepts/copilot-view-model-architecture.md) |
| 维护本地 MCP 集成 | [ColorVision MCP](./core-concepts/colorvision-mcp.md) |

## 开发前确认

- 当前主线是 Windows WPF，目标框架以 `net10.0-windows` 为主。
- ColorVision 桌面宿主运行时、官方插件和客户项目交付包当前仅支持 x64。
- 根目录存在 `ColorVision.snk` 时构建会启用强名称签名。
- 插件和项目包运行时进入主程序输出目录的 `Plugins/<Name>/`。
- 修改公开行为时，同步更新对应 README 或 `docs/` 页面。
- 修改打包/发布逻辑时，优先更新脚本文档和根目录贡献说明。

## 常用命令

```powershell
dotnet restore
dotnet build build.sln -p:Platform=x64
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64
dotnet test Test/ColorVision.Copilot.Tests/ColorVision.Copilot.Tests.csproj -c Release -p:Platform=x64
npm run docs:build
npm run docs:validate
```

插件和项目包打包：

```powershell
Scripts\package_plugin.bat Spectrum
Scripts\package_project.bat ProjectLUX
```

正式发布：

```powershell
Scripts\release.bat
```

## 平台支持策略

当前支持边界是 Windows x64。`build.sln`、主安装器、全量/增量更新包、官方插件包和
`runtimes/win-x64/native` 资产均按 x64 验证。ARM64 目前不受支持；不要使用
`-p:Platform=ARM64`、`-p:PlatformTarget=ARM64` 或任意包含 `arm64` 的 RID 生成宿主、插件或项目交付物。共享 MSBuild 策略会在初始化阶段以及 Build/Pack 入口 fail-fast。

`build.sln`、`scgd_general_wpf.sln` 和 `UI/UI.sln` 仍保留 `Any CPU`/`x86` 作为历史
IDE 与独立维护别名：其中不少 managed 项实际映射到 x64，部分 x86 配置还混合 Win32
native 项目。这些配置不进入 CI、安装器或发布脚本，不是受支持的宿主交付目标。

`ColorVision.FileIO` 是例外的独立纯托管 NuGet 包，但其规范产物固定为单一 AnyCPU
程序集和同一包坐标，不接受 `PlatformTarget` 或 RID 覆盖，也不生成 x64/ARM64 变体。
发布门禁会核对 nupkg 坐标、全部 PE 资产和 CLR flags。AnyCPU 包可被不同架构进程消费，
并不表示 ColorVision 桌面宿主或官方插件已经支持 ARM64。

```powershell
python Scripts\verify_platform_policy.py
```

真正增加 ARM64 支持必须分阶段完成：

1. 为全部必需的 C++、COM、OpenCV、设备厂商和算法 DLL 提供 ARM64 构建或受支持替代，并完成 ABI 测试；CUDA 能力必须有厂商支持的 Windows ARM64 技术路径，不能用 x64 DLL 代替。
2. 消除源码、项目引用和复制脚本中的 x64 固定路径，为 NuGet/插件/项目包增加独立的 `win-arm64` 资产。
3. 增加 ARM64 solution 配置和 CI 交叉编译，并在 Windows ARM64 设备上验证启动、插件加载、图像算法、设备通信和更新回滚。
4. 生成独立 ARM64 安装器与更新源，验证安装、升级、卸载和包签名后，才能把 ARM64 列为受支持平台。

## 目录说明

| 目录 | 内容 |
| --- | --- |
| `core-concepts/` | 扩展性、MCP/Copilot 等核心概念 |
| `engine-development/` | Engine、服务、模板、MQTT、OpenCV 接入 |
| `plugin-development/` | 插件接口、manifest、生命周期、打包 |
| `deployment/` | 安装器、自动更新和交付路径 |
| `scripts/` | 构建、打包、上传、发布脚本 |
| `backend/` | 插件市场后端 |

## 维护原则

- 开发文档写“怎么做”和“在哪里改”，不堆历史会议材料。
- 细节能回到源码、项目文件、manifest、脚本或测试命令。
- 一次性记录和临时计划不作为长期文档保留；需要时从 Git 历史或发布记录找回。
