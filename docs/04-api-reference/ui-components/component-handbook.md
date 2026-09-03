---
knowledge_id: "ui.package-boundaries"
knowledge_type: "reference"
status: "current"
summary: "UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。"
aliases: ["应该改哪个UI类库","UI包依赖","UI类库版本","目标框架兼容","ColorVision.Algorithms","ColorVision.Common","ColorVision.Core","ColorVision.ImageTools"]
code_paths: ["UI/Directory.Build.props","UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj","UI/ColorVision.Common/ColorVision.Common.csproj","UI/ColorVision.Core/ColorVision.Core.csproj","UI/ColorVision.Database/ColorVision.Database.csproj","UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj","UI/ColorVision.ImageTools/ColorVision.ImageTools.csproj","UI/ColorVision.Rbac/ColorVision.Rbac.csproj","UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj","UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj","UI/ColorVision.Solution/ColorVision.Solution.csproj","UI/ColorVision.Themes/ColorVision.Themes.csproj","UI/ColorVision.UI/ColorVision.UI.csproj","UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj"]
test_paths: ["Scripts/tests/test_algorithm_package_contract.py","Scripts/tests/test_verify_platform_policy.py"]
related: ["ui.index","ui.publishing","algorithms.platform","delivery.index"]
---

# UI 包职责与依赖边界

本页用于判断共享能力应放在哪个 UI 包，以及公开接口、依赖、框架或版本变化会影响哪些消费者。具体控件位置见[组件目录](./control-catalog.md)，装配问题见[运行时发现](./ui-runtime-handoff.md)，构建与发布步骤见[UI DLL 发布](./publishing.md)。

先按下表确定职责，再读取对应主题及目标 `.csproj`；包名或界面所在窗口不直接决定业务归属。

## 包边界

| DLL / 包 | 主要职责 | 常见风险 | 详细页 |
| --- | --- | --- | --- |
| `ColorVision.Common.dll` | MVVM 基础、共享接口、初始化器、状态栏契约、权限基础对象、Win32 工具 | 被高层业务反向污染 | [ColorVision.Common](./ColorVision.Common.md) |
| `ColorVision.Themes.dll` | 主题资源字典、窗口基类、标题栏、通用控件 | 资源缺失、主题枚举和实际 XAML 不一致 | [ColorVision.Themes](./ColorVision.Themes.md) |
| `ColorVision.UI.dll` | 配置、菜单、插件装载、属性编辑器、热键、多语言、日志、状态栏 | 插件加载成功不等于菜单/设置/状态栏都注册成功 | [ColorVision.UI](./ColorVision.UI.md) |
| `ColorVision.Algorithms.dll` | 中立算法身份、参数、调用、结果、目录与执行器 | 把 WPF、OpenCV、Engine DAO 或客户判定混入中立契约 | [算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) |
| `ColorVision.Core.dll` | `HImage`、OpenCV helper P/Invoke、CUDA/fusion bridge、WPF 位图桥接 | native DLL、x64 runtime 或 OpenCV 依赖漏包 | [ColorVision.Core](./ColorVision.Core.md) |
| `ColorVision.Database.dll` | SqlSugar DAO、MySQL/SQLite 基础、`GenericQueryWindow` 实体查询 | 实体和真实表结构不一致、连接配置错误 | [ColorVision.Database](./ColorVision.Database.md) |
| `ColorVision.SocketProtocol.dll` | TCP server、JSON/Text 分发、消息 SQLite、Socket 管理窗口 | 端口冲突、协议模式错误、Handler 未加载 | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) |
| `ColorVision.Scheduler.dll` | Quartz 调度、任务配置、执行历史、任务管理窗口 | 任务程序集未被发现、Cron/历史库不一致 | [ColorVision.Scheduler](./ColorVision.Scheduler.md) |
| `ColorVision.ImageEditor.dll` | `ImageView`、绘图图元、工具发现、结果 overlay、伪彩、CIE、3D、实时图像 | 工具初始化副作用、overlay 坐标和图像缩放不一致 | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) |
| `ColorVision.UI.Desktop` | 设置、向导、插件市场、下载器、第三方应用、反馈和诊断窗口 | 被误认为主程序入口；实际主程序仍在 `ColorVision/` | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) |
| `ColorVision.Solution.dll` | 工作区、文件树、编辑器、AvalonDock、终端 | 把 Engine 流程或客户业务塞进工作区壳层 | [ColorVision.Solution](./ColorVision.Solution.md) |
| `ColorVision.ImageTools.dll` | 多图查看、缩略图缓存、景深融合和 Solution 菜单贡献 | 把通用图像工具重新耦合进 Solution | [ColorVision.ImageTools](./ColorVision.ImageTools.md) |
| `ColorVision.Rbac.dll` | 本地账户、角色、权限、会话和审计窗口 | 把细权限误写成全产品统一网关 | [RBAC 模块](../../03-architecture/security/rbac.md) |

## 依赖方向

项目引用与运行调用是两种关系。当前主项目文件中，`ImageEditor` 引用 `Algorithms`、`Core`、`Common`、`Themes` 和 `UI`；`Solution` 引用 `ImageEditor`、`UI.Desktop` 和 `UI`；`ImageTools` 再引用 `Solution`、`ImageEditor` 等库。新增图像工具应放在其能力所有者，不能因为菜单显示在工作区就把实现移入 `Solution`。

`Algorithms` 没有项目或第三方包引用，也不启用 WPF。框架中立契约由它维护，像素来源、窗口、图像适配与 overlay 渲染由宿主承担；具体分界见[算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)。客户判定、MES 与业务导出仍属于 `Projects/`，Engine 历史结果的 DAO/handler 不能进入中立算法包。

依赖核对应读取当前 `.csproj` 的 `ProjectReference`、`PackageReference` 及其条件，不从示意关系推断直接引用。例如 `Themes` 引用 HandyControl，没有 `Common` 项目引用；`Core` 的 native 项目引用取决于 `UseProjectReference`，托管项目未引用 C++ 工程也不表示运行不需要 native DLL。底层库不应反向依赖高层窗口、Engine 业务或客户项目；确有共享能力时先确定接口、事件或 provider 边界。

## 框架、版本与产物检查

| 检查项 | 判断依据 |
| --- | --- |
| 目标框架 | 验证消费方与包内资产兼容，不要求所有 `TargetFramework(s)` 字符串等于主程序。部分 UI 库同时面向 .NET 8/10 Windows，`Algorithms` 面向 `net8.0;net10.0`，`ImageEditor` 等窗口库使用自己的 Windows TFM |
| 平台 | 中立 TFM 不解除仓库的宿主 x64 策略；FileIO 的独立 AnyCPU 例外见[构建平台与制品边界](../../02-developer-guide/README.md) |
| 版本与签名 | 按根 props、`UI/Directory.Build.props` 和项目覆写后的最终值判断，`Themes` 有自己的版本；不要求 UI 包与主程序版本号相同。保留存在签名密钥时的强名称规则，公开签名变更另核对实际消费者 |
| 资源与依赖 | 核对 NuGet 资产和宿主输出中需要的 XAML、图标、shader、CIE 数据及 native runtime；项目引用存在不证明依赖和资源已交付。插件共享文件去重由插件打包规则负责 |
| 运行时装配 | 对应程序集加载后，菜单、设置、状态栏、图像工具或 Socket handler 仍要经过各自发现与过滤；包生成成功不代表入口可见 |

发布预检、包顺序、NuGet 版本占用及消费方验证统一见[发布契约](./publishing.md)。选择验证范围时先确认此次任务允许的操作：源码/包检查不需要启动主程序；窗口、设备与外部服务的验证使用对应主题的前提和完成条件。

## 验证入口与缺口

关联测试：`Scripts/tests/test_algorithm_package_contract.py`、`Scripts/tests/test_verify_platform_policy.py`。

包契约与平台规则不覆盖所有 UI 行为；项目引用与目标框架以当前 csproj 为准，公开签名修改需验证实际消费者。
