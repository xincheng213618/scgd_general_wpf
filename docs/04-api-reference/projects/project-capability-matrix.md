---
knowledge_id: "projects.capabilities"
knowledge_type: "reference"
status: "current"
summary: "比较 ARVRPro、KB、LUX 和 IntegrationDemo 的检测触发、测试完成判据、流程配置与结果出口。"
aliases: ["哪个项目使用 Modbus","哪些项目输出 MES 或 Socket","ProjectARVRPro","ProjectKB","ProjectLUX","ProjectARVRPro.IntegrationDemo","项目协议对比","项目结果出口","项目流程配置","测试完成确认"]
code_paths: ["Projects/ProjectARVRPro/","Projects/ProjectARVRPro.IntegrationDemo/","Projects/ProjectKB/","Projects/ProjectLUX/"]
test_paths: ["Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj","Test/ProjectKB.Tests/ProjectKB.Tests.csproj","Test/ProjectLUX.Tests/ProjectLUX.Tests.csproj"]
related: ["projects.index","projects.arvr-pro","projects.arvr-pro-demo","projects.kb","projects.lux"]
---

# 项目横向速查

按业务、外部协议、流程配置或结果去向比较客户项目时使用本页。先确定现场实际启用的入口，再进入对应项目主题查看步骤、字段和失败处理；项目包与独立示例的装载、源码归属见[客户项目入口](./README.md)。

## 业务与触发方式

| 项目 | 业务定位 | 入口与协议 | 完成确认 |
| --- | --- | --- | --- |
| [ProjectARVRPro](./project-arvr-pro.md) | AR/VR 流程组、Recipe、切图与光学测试 | Socket JSON：`ProjectARVRInit`、`RunAll`、`SwitchGroup`；另有雷鸟串口切图、AOI Relay 对接 | `RunAll Code=0` 只表示接受启动；等待最终 `ProjectARVRResult` 并检查业务判定。详细时序见[ARVRPro 对接契约](./project-arvr-pro-integration-demo.md) |
| [ProjectKB](./project-kb.md) | 键盘背光亮度、均匀性、局部对比度及背光修正 | Modbus TCP 触发检测；MES DLL 用于 SN 检查与结果回传 | 寄存器回 `0` 也可能是忽略空 SN 触发；需分别核对检测结果、PLC 读回和已启用的 MES 输出 |
| [ProjectLUX](./project-lux.md) | 亮度、色彩、MTF、畸变、VID、光通量等测试 | 文本 Socket `T00XX,SN;`；普通 Flow 按活动组的 `SocketCode` 分派，VID/光通量有专用路径 | `T0000` 仅握手；命令响应不能单独证明 Flow 执行或 Recipe 判定通过。字段与异常响应见[LUX 协议](./project-lux-protocol.md) |
| [IntegrationDemo](./project-arvr-pro-integration-demo.md) | 独立 ARVRPro TCP/JSON 对接客户端 | 主动连接服务端，默认端口 `6666`；支持离线解析样例、联机命令及切图确认 | 联机测试等待最终结果；离线解析成功只说明样例可读取，不证明宿主、设备或协议联调通过 |

KB 状态栏的 Socket 配置与 Modbus 不是两条等效检测入口。当前 TCP 接收类尚未接入项目启动和检测事件，具体边界见 [KB 外部集成](./project-kb.md#外部集成)。

## 结果出口

| 输出 | 所属项目 | 实现定位 | 核对内容 |
| --- | --- | --- | --- |
| 整组/整机 CSV | ARVRPro、LUX | 各项目 `ObjectiveTestResult`、CSV exporter 与结果保存代码 | 文件名、目录、列顺序、单位、判定及旧格式兼容；两项目的同名类型不代表相同 schema |
| 本地 SQLite | ARVRPro、LUX、KB | 各自 `ViewResultManager`；KB 主结果为 `KBItemMaster` | SN、时间、批次、流程及结果详情；项目本地库不代替 Engine 原始批次与算法数据 |
| 客户 XLSX | ARVRPro | `Exports/CustomTestResultExportService.cs` | 当前输出 profile、工作表字段、路径与依赖 |
| MES | KB | `MesDll.cs`、`ProjectKBWindow.Processing()`、`Summary` | `CheckWIP` 与 `Collect_test` 的调用条件、参数和客户返回约定 |
| Socket 响应 | ARVRPro、LUX | 各自 `Services/SocketControl.cs`、handler 与窗口结果发送代码 | ACK、最终事件、SN、标准/Legacy 数据结构及失败响应 |
| 单次 CSV、文本与 summary | KB；Demo 另存解析结果 JSON/CSV | KB 的 `KBItemMasterExtensions.cs`、`ViewResultManager.Config`；Demo 的 `Program.cs` | 实际保存开关、路径、动态列及结果来源；客户端导出不是服务端业务结果落库 |

修改结果字段时，沿对应项目的所有已启用出口核对。例如 ARVRPro 的标准 CSV、Legacy、Socket `Data` 和客户 XLSX，KB 的 CSV、summary、MES 与本地结果可能使用不同转换逻辑。字段定义只在所属项目主题维护，本表用于定位受影响的出口。

## 流程配置差异

| 项目 | 组织方式与配置 | 需要区分的字段或范围 |
| --- | --- | --- |
| ARVRPro | `ProcessGroup`、`ProcessMeta`、`ProcessGroups.json`、`PictureSwitchConfig` | 流程组、步骤处理类型、Recipe 与切图设置共同决定执行；迁移和实例配置见[流程与 Recipe](./project-arvr-pro-processes.md) |
| LUX | `ProcessGroup`、`ProcessMeta`、`ProcessGroups.json`，另有 Recipe/Fix 配置 | `Name` 是显示名，`SocketCode` 匹配命令，`FlowTemplate` 绑定 Flow 模板；同一处理类型的 Recipe/Fix 共享，不能套用 ARVRPro 的实例配置方式 |
| KB | 当前 Flow 模板与按模板名选择的 `RecipeManager` / `KBRecipeConfig` | 模板名、POI 键名/宽度、KB 结果键名和 Recipe 快照需对应；不使用 ARVRPro/LUX 的流程组模型 |

普通 LUX 命令先按 `SocketCode` 查找步骤，再根据 `FlowTemplate` 查找模板。仅修改显示名称不会修改这两个绑定；重复命令码、缺失绑定和模板重命名分别按 [LUX 流程配置](./project-lux.md)排查。

## 交付与验证

1. 先用对应项目页列出的自动化测试、样例和构建入口验证改动范围。测试项目存在或离线解析成功不代表现场联调完成。
2. 真实 Modbus、MES、Socket、切图和设备测试会推进生产动作或写入外部系统；确认操作范围后，使用同一 SN/时间关联最终结果与已启用的输出。不能仅凭 ACK、寄存器归零或文件出现放行。
3. 交付前核对配置与依赖：ARVRPro 关注流程组、切图、Recipe 和结果格式；KB 关注 `FunTestDll.dll`、Modbus、MES 与 Recipe；LUX 关注 `SocketCode`、FlowTemplate、Recipe/Fix 和输出目录。

本地构建可能通过 `PostBuild` 把程序集和元数据复制到宿主的插件输出目录，不能理解为只修改项目自己的 `bin`。构建与上传、`.cvxp` 项目包与独立 Demo 的发布路径统一见[构建和交付边界](./README.md#本地构建与发布不是同一动作)，具体命令以各项目页为准。

客户专用逻辑留在拥有该业务的项目。跨项目变化再更新本表；单个协议、参数或结果字段变化，直接更新其权威主题，避免在多张表中重复维护同一契约。
