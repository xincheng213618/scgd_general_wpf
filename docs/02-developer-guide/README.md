---
knowledge_id: "delivery.index"
knowledge_type: "topic"
status: "current"
summary: "定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。"
aliases: ["开发入口","改代码","交付","x64","AnyCPU","ARM64","平台支持"]
code_paths: ["Directory.Build.props","ColorVision.PlatformPolicy.targets","build.sln","Engine/ColorVision.FileIO/ColorVision.FileIO.csproj",".github/workflows/dotnet.yml","Scripts/release.bat","Scripts/verify_platform_policy.py"]
test_paths: ["Scripts/tests/test_verify_platform_policy.py"]
related: ["platform.system","delivery.prerequisites","delivery.testing","delivery.scripts"]
---

# 构建平台与制品边界

本页规定 ColorVision 宿主、插件、客户项目和独立 FileIO 包的平台与制品组合。具体模块从[源码知识地图](../knowledge/index.md)定位；修改共享构建策略时，分别核对宿主、原生依赖和独立包。

## 先区分动作和制品

| 当前任务 | 权威入口与边界 |
| --- | --- |
| 理解或修改源码 | 先定位实际模块及其测试；不要求安装设备驱动、启动主程序或发布 |
| 首次准备构建 | [环境与依赖](../00-getting-started/prerequisites.md)核对 native helper、C++ 工具链、x64 与签名条件 |
| 选择验证范围 | [测试与验证](./testing.md)给出最小检查及覆盖缺口；文档检查不能替代产品测试 |
| 构建或发布交付包 | [脚本契约](./scripts/README.md)定义入口和副作用；打包 wrapper 可能上传，不能作为默认本地构建 |
| 修改公开行为或平台策略 | 同步对应主题与代码映射，按[维护规范](../knowledge/maintenance.md)复核；不要另写一份面向不同角色的说明 |

## 平台支持策略

当前支持边界是 Windows x64。`build.sln`、主安装器、全量/增量更新包、官方插件包和 `runtimes/win-x64/native` 资产均按 x64 验证。
宿主、官方插件和客户项目的 `Platform`/`PlatformTarget` 必须是 `x64`；RID 可以为空或为 `win-x64`，多 RID 只能包含 `win-x64`。
x86、AnyCPU、ARM64、`win-x86`、`linux-x64` 和混合 RID 等显式覆盖都会由共享 MSBuild 策略在初始化阶段以及 Build/Pack 入口 fail-fast。

`build.sln`、`scgd_general_wpf.sln` 和 `UI/UI.sln` 只公开 `Debug|x64` 与 `Release|x64`。除 `ColorVision.FileIO` 外，所有 managed 与 native 项目都在这两个配置下映射到 x64。主程序 CI 构建和正式交付使用 `Release|x64`；本地开发可以使用 `Debug|x64`。

`ColorVision.FileIO` 是唯一例外的独立纯托管 NuGet 包。包含它的 solution 在 x64 solution 配置下将该项目的 `ActiveCfg` 与 `Build.0` 映射到 `Any CPU`。
规范产物位于无架构目录的 `Engine/ColorVision.FileIO/bin/Release`，并使用同一包坐标，各目标框架内均为纯托管 AnyCPU 程序集；它要求 `Platform=AnyCPU`、`PlatformTarget=AnyCPU` 且 RID 为空，不生成 x64/x86/ARM64 变体。
发布门禁会核对 nupkg 坐标、全部 PE 资产和 CLR flags。AnyCPU 包可被不同架构进程消费，
并不表示 ColorVision 桌面宿主或官方插件已经支持 ARM64。

```powershell
# 只读核对项目、solution 和策略声明；不构建、不启动应用或发布
python Scripts\verify_platform_policy.py
```

不带包参数时，此命令不检查已生成的 `.nupkg`。CI 使用 `--fileio-package-directory Engine/ColorVision.FileIO/bin/Release` 另核对 FileIO 包坐标、目标框架和 PE/CLR 标志；声明检查通过不能替代实际包验证。

真正增加 ARM64 支持必须分阶段完成：

1. 为全部必需的 C++、COM、OpenCV、设备厂商和算法 DLL 提供 ARM64 构建或受支持替代，并完成 ABI 测试；CUDA 能力必须有厂商支持的 Windows ARM64 技术路径，不能用 x64 DLL 代替。
2. 消除源码、项目引用和复制脚本中的 x64 固定路径，为 NuGet/插件/项目包增加独立的 `win-arm64` 资产。
3. 增加 ARM64 solution 配置和 CI 交叉编译，并在 Windows ARM64 设备上验证启动、插件加载、图像算法、设备通信和更新回滚。
4. 生成独立 ARM64 安装器与更新源，验证安装、升级、卸载和包签名后，才能把 ARM64 列为受支持平台。

## 证据与变更规则

`Directory.Build.props`、各项目文件和 solution 映射共同决定实际平台策略。`Scripts/verify_platform_policy.py` 及其测试用于检查策略；测试文件存在不代表已在本轮运行，也不证明真机驱动、安装升级和设备行为。

普通构建与检查不授权签名上传、发布、提交或推送。发布执行规则仍以根 `AGENTS.md` 和[脚本契约](./scripts/README.md)为准，不在本页复制另一套命令清单。
