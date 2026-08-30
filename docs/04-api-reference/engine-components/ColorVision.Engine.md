---
knowledge_id: "engine.host"
knowledge_type: "topic"
status: "current"
summary: "ColorVision.Engine工程的条件引用、NuGet/DLL依赖回退与资源打包；schema嵌入程序集，缺少输出散文件不等于漏包，也不保证脱离UI源码独立构建。"
aliases: ["Engine是不是本地算法库", "ColorVision.Engine", "ColorVision.Engine.csproj", "Engine工程依赖", "资源打包", "EmbeddedResource", "UIProjectPackageVersion"]
code_paths: ["Engine/ColorVision.Engine/ColorVision.Engine.csproj", "Directory.Build.props"]
test_paths: []
related: ["engine.index", "engine.native-integration", "algorithms.json-templates", "delivery.prerequisites", "delivery.scripts"]
---

# ColorVision.Engine 工程、资源与依赖

`Engine/ColorVision.Engine/` 是连接设备、模板、MQTT、Flow、结果展示和编辑 UI 的宿主工程，不是纯本地算法库。本页回答依赖解析、构建前提和资源打包问题；运行契约直接从 [Engine 入口](./README.md) 进入负责该能力的主题，不把所有模块串成一条必然执行的启动链。

## 工程属性的来源

`ColorVision.Engine.csproj` 启用 WPF、WindowsForms 和 unsafe，声明 `OutputType=WinExe`。目标框架从根 `Directory.Build.props` 继承，当前为 `net10.0-windows`、x64；签名也由根属性按 `ColorVision.snk` 是否存在决定，有密钥时不能为绕过构建问题禁用签名。

工程有自己的 `VersionPrefix` 声明，不能直接把根目录的主程序版本当作 Engine 程序集版本。查询版本与依赖时读取当前工程和导入属性，不在本页维护另一份会过期的版本清单。`WinExe` 声明也不证明脱离 ColorVision 的配置、资源和其他项目后可以独立运行。

## 条件引用不等于独立构建保证

以下是本工程直接声明的依赖选择，完整运行环境还包含传递依赖：

| 依赖组 | 源码存在时 | 源码不存在时 |
| --- | --- | --- |
| `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.ImageEditor`、`ColorVision.Scheduler`、`ColorVision.Solution`、`ColorVision.UI` | 引用 `UI/` 中对应 `.csproj` | 同名 NuGet 包，版本取 `UIProjectPackageVersion` |
| `FlowEngineLib`、`ST.Library.UI` | 引用 `Engine/` 中对应 `.csproj` | 引用 `DLL/` 中同名 DLL，`Private=True` |
| `ColorVision.UI.Desktop`、`ColorVision.FileIO`、`cvColorVision` | 无条件项目引用 | 本工程没有对应包或 DLL 回退分支 |

选择条件检查的是项目文件是否存在，不是“源码编译失败后自动换成包”。因此删除 `UI/` 目录不能保证只依赖 NuGet 就能构建；DLL 回退也要求实际文件及兼容依赖存在。`UIProjectPackageVersion` 当前默认 `*`，若需可重现的包输入，应核验最终解析结果，不能仅凭这个属性宣称版本已锁定。

本工程还显式引用 `OpenCvSharp4.runtime.win`。native helper 的首次构建、ABI 和运行 DLL 前提见 [OpenCV/native 集成](../../02-developer-guide/engine-development/opencv-integration.md)，不要把一次托管构建等同于完整的 native 运行验证。

## 资源不是同一种输出文件

| 工程声明 | 当前资源 | 验证方式与边界 |
| --- | --- | --- |
| WPF `Resource` | `Assets/Image/` 中列出的图标、背景和 `Assets/png/PowerToy.png` | 检查 WPF 资源与实际使用路径，不要求在输出目录出现同名散文件；背景明确为 `CopyToOutputDirectory=Never` |
| `EmbeddedResource` | `Templates/Jsons/**/*.schema.json` 与 `Templates/Jsons/Schemas/schema-index.json` | 检查程序集清单资源及 `LogicalName`，不是检查文件是否被复制到输出目录 |
| 工具 EXE / 外部运行文件 | 本工程没有通用“工具 EXE 自动复制”声明 | 追踪实际所属项目或交付脚本，不能从上述资源项推断工具已打包 |

Schema 的逻辑资源名使用 `Templates/Jsons/…` 前缀；索引固定为 `Templates/Jsons/Schemas/schema-index.json`。这两类 JSON 从 `None` 移除后作为嵌入资源编译。编辑器如何按 Code 查索引、何时回退磁盘、找不到时如何处理，统一见 [JSON 模板的 Schema 查找](../algorithms/templates/json-templates.md#schema-查找与发布边界)。

## 最小验证与缺口

纯源码问答先核对上述 `.csproj`、根属性及消费资源的代码，不需要启动产品。确实要验证工程构建时，先满足 [Windows/x64 环境前提](../../00-getting-started/prerequisites.md) 和 native 集成条件，再从仓库根目录执行：

```powershell
dotnet build .\Engine\ColorVision.Engine\ColorVision.Engine.csproj -c Release -p:Platform=x64
```

该命令会生成本地产物，隐式 restore 在缺少依赖时可能访问包源；不是发布命令，不应用于仅文档改动的验收。成功编译不能证明 schema 可被每个编辑器命中、图标可显示、MySQL/broker/设备可用或交付目录完整。

本页没有声明覆盖这些工程与资源事实的自动化测试。Flow 最终化和设备配置的局部测试应留在对应主题，不能作为工程资源验收；资源变更需按实际构建产物与消费入口补验，外部设备和上传仍需单独授权。
