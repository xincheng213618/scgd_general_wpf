# Engine 业务链路矩阵

这页用于 Engine 对接、排障和改需求时快速定位“业务能力、代码入口、配置来源、验证方法”。先用本页确定问题属于设备、模板、Flow、结果、项目包还是发布验证，再进入对应链路页深挖。

Engine 的核心链路是：启动初始化 -> 资源与设备服务 -> MQTT/远程命令 -> 模板系统 -> Flow 模板 -> FlowEngine 执行 -> 算法结果 -> 图像展示/Overlay -> 项目包判定/导出。

## 核心链路

| 场景 | 主要代码入口 | 先查什么 |
| --- | --- | --- |
| 启动初始化 | `MySqlInitializer`、`MqttInitializer`、`ServiceInitializer`、`TemplateInitializer`、`RCInitializer` | 初始化顺序、异常日志、数据库、MQTT、模板扫描 |
| 设备服务生成 | `ServiceManager`、`TypeService`、`DeviceServiceFactoryRegistry`、`DeviceService` | `ServiceTypes`、资源 `type/pid/is_delete/tenant_id`、工厂注册、`DeviceServices` |
| MQTT 与远程命令 | `MQTTControl`、`MQTTConfig`、`Services/Devices/*/MQTT*.cs`、`MQTTRCService` | 连接状态、topic、返回码/结果 ID、文件下载、服务端日志 |
| 模板加载 | `TemplateContorl.cs`、`TemplateModel.cs`、`Jsons/**/Template*.cs`、`TemplatePoi.cs` | 类是否扫描、无参构造、`Code`、`TemplateDicId`、模板参数 |
| Flow 模板保存与导入 | `TemplateFlow`、`FlowPackageHelper`、`ModMasterModel`、`ModDetailModel`、`SysResourceModel` | `DataBase64`、关联模板、重名处理、资源 `ValueA/Value` |
| Flow 执行 | `FlowEngineControl`、`BaseStartNode`、`CVEndNode`、`FlowControl` | Start 节点、节点状态、设备 token、`FlowCompleted` |
| 节点业务绑定 | `Templates/Flow/Nodes/`、`NodeConfiguratorRegistry`、各类 `*NodeConfig.cs` | 配置器、设备/模板名、参数恢复、是否只改了 `FlowEngineLib` |
| 算法结果展示 | `ViewResultAlg`、`IViewResult`、`IResultHandlers`、`ViewHandle*.cs` | handler 反射、`CanHandle`、结果 ID、图像路径和 overlay 坐标 |
| 项目包业务 | `Projects/*/Process/`、`Recipe/`、`Fix/`、`ObjectiveTestResult.cs` | Engine 原始值、项目 key、Recipe/Fix、导出字段 |
| 文件与图像 | `FileServer`、`Media/`、`ColorVision.FileIO`、`cvColorVision` | 文件路径、权限、格式读取器、native DLL、图像尺寸和坐标系 |

## 设备类型入口

| 设备类型 | Engine 目录 | 最小验证 |
| --- | --- | --- |
| Camera | `Services/Devices/Camera/`、`Services/PhyCameras/` | Camera 服务生成，实时/拍照返回文件，ImageEditor 可打开 |
| PG | `Services/Devices/PG/` | MQTT 返回成功，屏幕状态和流程节点状态一致 |
| Spectrum | `Services/Devices/Spectrum/` | 采集有结果，曲线和 CIE 值能显示，项目导出字段有值 |
| SMU | `Services/Devices/SMU/` | 连接正常，读数能落到结果或项目字段 |
| Sensor | `Services/Devices/Sensor/` | 设备在线，状态变化能刷新到 Flow 或 UI |
| FileServer | `Services/Devices/FileServer/`、`Services/Cache/` | 下载路径存在，权限正常，缓存清理不误删当前结果 |
| Algorithm | `Services/Devices/Algorithm/`、`Templates/Jsons/`、`Templates/ARVR/` | 返回结果 ID，DAO 能查结果，`ViewHandle` 能显示 |
| Calibration | `Services/Devices/Calibration/` | 标定参数能加载，结果能写回后续算法需要的位置 |
| Motor/CfwPort/FlowDevice/ThirdPartyAlgorithms | 对应 `Services/Devices/*/` | 命令协议、状态同步、文件路径和结果解析一起验证 |

新增设备时同时回答四个问题：资源类型怎么建、服务怎么生成、Flow 节点怎么绑定、结果或状态怎么回到 UI/项目包。

## 变更归属

| 需求 | 优先修改位置 | 不建议放在哪里 | 同步文档 |
| --- | --- | --- | --- |
| 新增设备类型 | `TypeService`、`Services/Devices/`、`DeviceServiceFactoryRegistry` | 项目包窗口里手动 new 设备 | [设备服务链路](./device-service-chain.md)、使用手册设备页 |
| 新增 Flow 节点 | `FlowEngineLib/`、`Templates/Flow/Nodes/`、`NodeConfigurator/` | 只改节点 UI，不补配置器 | [模板与 Flow 链路](./template-flow-chain.md)、[Flow 节点扩展](../extensions/flow-node.md) |
| 新增算法参数或模板 | `Templates/Jsons/`、`ARVR/`、`POI/` 或对应模板目录 | `Projects/*/Process/` 里拼临时 JSON | [算法与模板](../algorithms/README.md) |
| 修改结果 overlay | `ViewHandle*.cs`、`IResultHandlers.cs`、`ImageEditor/Draw/` | 项目包导出器 | [结果展示链路](./result-handoff-chain.md)、UI 组件目录 |
| 修改客户判定或字段 | `Projects/<Project>/Recipe/`、`Fix/`、`Process/`、导出器 | Engine 通用 result handler | [项目说明](../../00-projects/README.md)、对应项目页 |
| 修改 Socket/MES | 项目 `SocketControl.cs` 或 `ColorVision.SocketProtocol` | 设备服务基类 | 项目包页、SocketProtocol 组件页 |
| 修改插件或 DLL 发布 | 插件 manifest、`.csproj`、`Directory.Build.props`、`Scripts/` | 文档配置硬写版本 | 插件开发手册、UI DLL 发布 |

## 排障入口

| 现象 | 第一入口 |
| --- | --- |
| 资源树没有设备分类 | `SysDictionaryModel`、`TypeService`、过滤类型值 |
| 设备资源存在但运行时没有服务 | `SysResourceModel.Type`、`DeviceServiceFactoryRegistry.CreateService()` |
| 流程能打开但节点参数丢失 | `TemplateFlow` 保存内容、`NodeConfiguratorRegistry` |
| `.cvflow` 导入后跑不起来 | 关联模板导入、重名模板、节点设备 Code |
| 算法成功但没有 overlay | 结果 ID、`IDisplayAlgorithm`、`IResultHandleBase`、`ViewResultAlgType` |
| 项目 CSV 字段为空 | Engine 原始结果、项目 `Process` key、`Recipe/Fix`、导出器字段 |

## 发布验收

| 变更类型 | 建议构建 | 手工冒烟 |
| --- | --- | --- |
| 设备服务 | `dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64` | 新建资源、刷新服务树、连接设备、执行一次设备命令 |
| 模板或算法 | 构建 Engine，必要时构建主程序 | 新建/编辑模板、运行一次算法、查看结果窗口 |
| Flow 节点 | 构建 `FlowEngineLib` 和 Engine | 打开流程、编辑节点、保存、重新打开、执行 |
| 结果展示 | 构建 Engine 和 ImageEditor | 打开历史结果，确认 overlay、坐标和缩放 |
| 项目包逻辑 | 构建对应 `Projects/<Project>` 和主程序 | 跑一条项目流程，检查 SQLite/CSV/MES 或 Socket |
| 文档站 | `npm run docs:build` | 打开对应 HTML，检查搜索索引关键术语 |

## 风险点

- `TemplateControl` 类所在文件名是 `TemplateContorl.cs`，不要误判为两套模板控制器。
- `TypeService` 枚举存在不代表 UI 一定显示；资源字典、过滤条件和工厂注册都要满足。
- 模板名和 `TemplateDicId` 冲突会影响 Flow 保存、导入和执行。
- `FlowEngineLib` 是执行骨架，Engine 的 `TemplateFlow` 和 `NodeConfigurator` 才是业务绑定层。
- `IResultHandleBase` 适合通用显示和 overlay，不适合塞客户项目判定规则。
- MySQL、MQTT、注册中心和文件服务器失败时，表面症状可能是“模板空、设备不见、流程无结果”。
