# 当前 Engine 文档覆盖清单

本页用来回答“Engine 里的业务逻辑是否有交接入口”。它不是逐文件 API 清单，而是把当前 `Engine/` 真实项目、`ColorVision.Engine` 关键目录和已有交接页对应起来，便于接手人员先看业务链，再进入源码。

## 当前覆盖结论

| Engine 项目 | 工程文件 | README | 当前文档页 | 交接入口 | 结论 |
| --- | --- | --- | --- | --- | --- |
| `Engine/ColorVision.Engine/` | `ColorVision.Engine.csproj` | 有 | [ColorVision.Engine](./ColorVision.Engine.md) | [业务链路矩阵](./business-flow-matrix.md)、[业务场景手册](./business-scenario-playbook.md)、[运行时对象目录](./runtime-object-map.md) | 主业务运行时覆盖完整 |
| `Engine/FlowEngineLib/` | `FlowEngineLib.csproj` | 有 | [FlowEngineLib](./FlowEngineLib.md) | [模板与 Flow 链路](./template-flow-chain.md) | Flow 执行链覆盖完整 |
| `Engine/cvColorVision/` | `cvColorVision.csproj` | 有 | [cvColorVision](./cvColorVision.md) | [结果展示与项目交接链路](./result-handoff-chain.md) | native/视觉边界已说明 |
| `Engine/ColorVision.FileIO/` | `ColorVision.FileIO.csproj` | 有 | [ColorVision.FileIO](./ColorVision.FileIO.md) | [数据导出与导入](../../01-user-guide/data-management/export-import.md)、结果链路 | 文件格式和导入导出已说明 |
| `Engine/ST.Library.UI/` | `ST.Library.UI.csproj` | 有 | [ST.Library.UI](./ST.Library.UI.md) | [模板与 Flow 链路](./template-flow-chain.md) | 节点编辑 UI 基础已说明 |
| `Engine/ColorVision.ShellExtension/` | `ColorVision.ShellExtension.csproj` | 无 | [ColorVision.ShellExtension](./ColorVision.ShellExtension.md) | Shell 缩略图扩展页、[ColorVision.FileIO](./ColorVision.FileIO.md) | 外部 Explorer 集成链路已说明 |

## `ColorVision.Engine` 业务目录覆盖

| 源码目录 | 业务含义 | 当前交接页 | 接手时先问什么 |
| --- | --- | --- | --- |
| `Services/` | 服务管理、设备服务基类、终端、缓存、RC 服务 | [设备服务链路](./device-service-chain.md)、[业务链路矩阵](./business-flow-matrix.md) | 资源是否能生成正确 `DeviceService` |
| `Services/Devices/` | Camera、Motor、SMU、FileServer、FlowDevice 等具体设备 | [设备服务链路](./device-service-chain.md) | 手动设备动作和 Flow 节点是否引用同一设备 |
| `Templates/` | 模板参数、Flow 模板、算法模板、POI/ROI、ARVR 模板 | [模板与 Flow 链路](./template-flow-chain.md)、[结果展示与项目交接链路](./result-handoff-chain.md) | 模板版本、节点绑定和结果映射是否一致 |
| `FlowEngineLib/Node/Algorithm/`、`FlowEngineLib/Algorithm/` | Flow 算法、转换、校准节点 | [Flow 转换与校准节点](./flow-conversion-calibration-nodes.md)、[模板与 Flow 链路](./template-flow-chain.md) | 节点的 `operatorCode`、参数对象和配置器是否一致 |
| `MQTT/` | MQTT 配置、连接、控制对象 | [设备服务链路](./device-service-chain.md)、[业务场景手册](./business-scenario-playbook.md) | 主题、连接状态和设备 Code 是否匹配 |
| `Batch/`、`Dao/`、`Mysql/` | 批次、结果记录、MySQL/SQLite 数据访问 | [结果展示与项目交接链路](./result-handoff-chain.md) | 数据是否已经落库，批次/SN 是否一致 |
| `Messages/` | MQTT 和业务消息模型 | [业务链路矩阵](./business-flow-matrix.md) | 外部系统和项目包使用的是哪类消息 |
| `Archive/`、`Reports/` | 归档结果查询和报表生成 | [结果展示与项目交接链路](./result-handoff-chain.md) | 结果来源、字段、文件路径和报表版本是否对应 |
| `ToolPlugins/` | 内置工具入口，例如 ImageJ、CVRaw 转 CSV | [业务场景手册](./business-scenario-playbook.md)、[ColorVision.Engine](./ColorVision.Engine.md) | 工具是调试辅助还是正式产线交付物 |
| `Abstractions/`、`PropertyEditor/`、`Utilities/` | 公共接口、属性编辑和工具类 | [运行时对象目录](./runtime-object-map.md) | 是否被业务链路直接调用 |
| `Assets/`、`Properties/`、`CalFile/`、`Media/` | 资源、属性、标定/媒体辅助文件 | 对应链路页按场景引用 | 文件是否需要随包复制、是否影响现场复测 |
| `bin/`、`obj/` | 构建输出和中间产物 | 不作为文档对象 | 不手工维护，不作为业务交接依据 |

## 交接阅读顺序

1. 不知道问题归属时，先看 [Engine 业务链路矩阵](./business-flow-matrix.md)。
2. 已经知道场景，例如设备新增、模板变更、Flow 失败、结果不显示，进入 [Engine 业务场景交接手册](./business-scenario-playbook.md)。
3. 已经完成或准备完成改动，进入 [Engine 变更影响与验收清单](./engine-change-impact-checklist.md) 收集交接证据。
4. 已经知道类名或运行时对象，进入 [Engine 运行时对象目录](./runtime-object-map.md)。
5. 已经知道链路类型，分别看 [设备服务链路](./device-service-chain.md)、[模板与 Flow 链路](./template-flow-chain.md)、[结果展示与项目交接链路](./result-handoff-chain.md)。
6. 涉及客户项目输出时，继续看 [项目包能力与交接矩阵](../projects/project-capability-matrix.md) 和具体项目页。

## 覆盖检查命令

```powershell
Get-ChildItem Engine -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem Engine/ColorVision.Engine -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/04-api-reference/engine-components -File | Sort-Object Name | Select-Object -ExpandProperty Name
```

新增 Engine 项目、设备类型、模板目录或结果链路时，必须同步更新本页、[Engine 业务链路矩阵](./business-flow-matrix.md)、[Engine 业务场景交接手册](./business-scenario-playbook.md)、[Engine 变更影响与验收清单](./engine-change-impact-checklist.md) 和对应链路页。
