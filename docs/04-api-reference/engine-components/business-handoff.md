# Engine 业务交接手册

这页给接手 Engine 的开发人员使用。目标不是讲完所有算法，而是把当前系统最容易踩错的业务链路说清楚。

如果你现在是按“要改哪个业务能力”来找入口，先看 [Engine 业务链路矩阵](./business-flow-matrix.md)。如果已经拿到具体需求或缺陷，先看 [Engine 业务场景交接手册](./business-scenario-playbook.md)。如果改动已经完成或准备交接，用 [Engine 变更影响与验收清单](./engine-change-impact-checklist.md) 确认影响面和证据。本页更适合在确定方向后，顺着完整执行链理解上下游。

## 一句话理解 Engine

Engine 负责把“设备资源、模板参数、流程节点、MQTT 命令、算法结果、图像展示、数据库记录”组织成可执行的检测业务。

主程序和项目包通常只做入口、界面、客户流程和结果包装；真正连接设备、模板和流程的核心在 Engine。

## 启动和初始化

当前启动时要关注三类初始化：

| 初始化对象 | 位置 | 作用 |
| --- | --- | --- |
| 插件加载 | `Engine/ColorVision.Engine/App.xaml.cs`、`UI/ColorVision.UI/Plugins/PluginLoader.cs` | 扫描 `Plugins/`，加载插件和项目包 |
| 模板初始化 | `Templates/TemplateContorl.cs` | 扫描并加载模板类型、模板参数 |
| 设备服务初始化 | `Services/ServiceManager.cs` | 根据资源模型生成设备服务实例 |

交接时先确认主程序是否能走完这三步。任何一步失败，都可能表现成菜单缺失、模板为空、流程节点找不到设备。

## 设备服务链路

更细的设备资源、工厂注册和显示页生成链路见 [Engine 设备服务链路](./device-service-chain.md)。

### 关键类

- `Services/ServiceManager.cs`
- `Services/DeviceService.cs`
- `Services/Devices/DeviceServiceFactory.cs`
- `Services/Devices/DeviceServiceConfig.cs`
- `Services/Type/TypeService.cs`

### 当前生成流程

1. 系统从资源配置或数据库中拿到 `SysResourceModel`。
2. `ServiceManager` 遍历资源。
3. `DeviceServiceFactoryRegistry.CreateService(sysResourceModel)` 根据 `ServiceTypes` 创建具体设备。
4. 设备加入 `ServiceManager.DeviceServices`。
5. UI、Flow 节点配置器、模板显示页从 `DeviceServices` 里筛选需要的设备类型。

### 新增设备的最小步骤

1. 在 `ServiceTypes` 增加服务类型。
2. 新建 `ConfigXxx : DeviceServiceConfig`。
3. 新建 `DeviceXxx : DeviceService<ConfigXxx>`。
4. 在 `DeviceServiceFactoryRegistry` 注册工厂。
5. 如需 Flow 节点配置，在 `Templates/Flow/NodeConfigurator/` 增加对应配置器。
6. 如需用户文档，在 `01-user-guide/devices/` 和本模块参考补说明。

不要只新建窗口或菜单。没有注册工厂时，资源能存在，但运行时不会生成稳定服务实例。

## MQTT 和设备命令

Engine 中的很多设备不是直接本地执行，而是通过 MQTT/服务端命令完成。

| 位置 | 作用 |
| --- | --- |
| `MQTT/MQTTConfig.cs` | MQTT 连接配置 |
| `MQTT/MQTTConnect.xaml.cs` | 连接窗口和默认配置切换 |
| `Services/Devices/*/MQTT*.cs` | 各类设备的 MQTT 命令封装 |
| `Messages/` | 业务消息模型 |

典型链路是：

1. UI 或流程节点调用设备服务方法。
2. 设备服务组织命令参数。
3. MQTT 服务发布命令到服务端。
4. 服务端返回文件、结果 ID 或状态。
5. Engine 查询结果或下载文件，再交给显示/项目层处理。

排查时不要只看 UI 按钮是否触发。要继续看 MQTT 是否连接、topic 是否正确、服务端是否返回结果、文件服务器是否可下载。

## 模板系统链路

更细的模板加载、Flow 保存、导入导出和节点配置器说明见 [Engine 模板与 Flow 链路](./template-flow-chain.md)。

### 关键类

- `Templates/TemplateModel.cs`
- `Templates/TemplateContorl.cs`
- `Templates/TemplateManagerWindow.xaml.cs`
- `Templates/TemplateEditorWindow.xaml.cs`
- `Templates/Jsons/*/Template*.cs`
- `Templates/POI/TemplatePoi.cs`
- `Templates/Flow/TemplateFlow.cs`

### 模板承担什么

模板不是 UI 表单的别名。它承担三个职责：

- 管理一组参数和名称。
- 为算法命令提供 `CVTemplateParam` 或 JSON 参数。
- 为流程模板提供可保存、可复制、可编辑的业务配置。

### 新增算法模板的最小步骤

1. 新建参数类，继承现有 `ParamBase` 或 `TemplateJsonParam`。
2. 新建模板类，实现 `ITemplate<T>`、`ITemplateJson<T>` 或相近基类。
3. 设置 `Title`、`Code`、`TemplateDicId`、`TemplateParams`。
4. 提供编辑控件，常见是 `EditTemplateJson` 或专用 UserControl。
5. 如需要菜单入口，继承 `MenuITemplateAlgorithmBase`。
6. 如需要结果展示，补 `AlgorithmXxx`、`ViewHandleXxx`、MySQL/DAO 读取逻辑。
7. 如需要 Flow 节点，补节点和节点配置器。

模板最容易出错的是 `TemplateDicId`、参数集合和结果解析不一致。新增时要从模板保存、流程执行、结果展示三处同时验证。

## FlowEngine 链路

### 关键类

- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/Nodes/`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/`

### 运行时理解

FlowEngine 负责跑节点，但 ColorVision 的业务流程不是只在 `FlowEngineLib` 里完成。真正落地时还要经过：

- `TemplateFlow` 保存和加载流程模板。
- Flow 节点绑定设备 `DeviceCode`、模板名或算法参数。
- 项目包选择某个流程模板并触发执行。
- `FlowCompleted` 后由项目包或批次页面继续解析结果。

因此排查流程问题要看三层：

1. 流程模板是否加载正确。
2. 节点里的设备/模板绑定是否有效。
3. Flow 完成后结果处理是否匹配当前项目包。

## 结果展示和业务结果

更细的 handler 扫描、overlay、`IViewResult` 和项目包结果映射见 [Engine 结果展示与项目交接链路](./result-handoff-chain.md)。

Engine 结果大致分成三类：

| 类型 | 位置 | 用途 |
| --- | --- | --- |
| 原始算法结果 | `Dao/`、各模板目录下的 `Mysql*.cs` | 从数据库或服务端读取算法结果 |
| 展示结果 | `Abstractions/IResultHandlers.cs`、`Templates/*/ViewHandle*.cs` | 给 ImageEditor 或 AlgorithmView 画 overlay |
| 项目业务结果 | `Projects/*/ObjectiveTestResult.cs`、`Process/*` | 给客户 CSV/PDF/MES/Socket 使用 |

同一个算法结果可能同时服务于通用展示和客户项目判定。不要在 Engine 结果 handler 里写客户特有规则；客户规则应放在项目包的 Process/Recipe/Fix 层。

## 项目包如何使用 Engine

项目包通常按这个模式工作：

1. 插件 manifest 让主程序加载项目包。
2. 菜单或 Socket 进入项目窗口。
3. 项目窗口选择流程组、Recipe、Fix、SN。
4. 调用 Engine 的流程模板执行。
5. 从 Engine 批次或算法结果中读取数据。
6. 项目自己的 Process 把结果映射成客户字段。
7. 输出 SQLite、CSV、PDF、Socket/MES 响应。

典型参考：

- `Projects/ProjectLUX/Process/`
- `Projects/ProjectARVRPro/Process/`
- `Projects/ProjectKB/`

## 常见修改怎么落点

| 需求 | 应该优先修改哪里 |
| --- | --- |
| 新增设备类型 | `Services/Devices/`、`DeviceServiceFactoryRegistry` |
| 新增流程节点 | `Templates/Flow/Nodes/`、`Templates/Flow/NodeConfigurator/`、`FlowEngineLib` |
| 新增算法模板 | `Templates/Jsons/` 或对应算法目录 |
| 修改模板参数编辑 | 对应 `Template*.cs` 和编辑控件 |
| 修改结果 overlay | `ViewHandle*.cs`、ImageEditor draw/overlay |
| 修改客户判定规则 | `Projects/<Project>/Recipe/`、`Fix/`、`Process/` |
| 修改 Socket 对接 | `Projects/<Project>/Services/SocketControl.cs` 或 `UI/ColorVision.SocketProtocol` |
| 修改批次/历史结果 | `Dao/`、项目包 `ViewResultManager` |

## 排查顺序

### 设备不显示

1. 资源是否存在。
2. `ServiceTypes` 是否匹配。
3. `DeviceServiceFactoryRegistry` 是否注册。
4. `ServiceManager.DeviceServices` 是否生成。
5. UI 或节点配置器筛选的设备类型是否正确。

### 流程能启动但不出结果

1. Flow 模板是否加载。
2. 节点设备 Code 是否有效。
3. MQTT 是否连接。
4. 算法服务是否返回结果。
5. 文件服务器是否能下载原始文件。
6. `FlowCompleted` 后项目包是否解析了正确模板名。

### 结果能查到但图像不画 overlay

1. 对应算法是否有 `ViewHandle` 或 result handler。
2. 结果类型是否匹配 `ViewResultAlgType`。
3. ImageEditor 是否收到正确文件路径。
4. overlay 坐标是否按当前图像尺寸转换。

### 项目 CSV/PDF 字段为空

1. Engine 原始结果是否有值。
2. 项目 `Process.Execute()` 是否读取了正确 key。
3. Recipe/Fix 是否把值修正成空或失败。
4. `ObjectiveTestResult` 字段是否和导出器字段一致。

## 文档维护约定

- 横向业务入口、变更归属、发布验收规则改动后，同步更新 [Engine 业务链路矩阵](./business-flow-matrix.md)。
- Engine 业务改动准备交接时，同步更新 [Engine 变更影响与验收清单](./engine-change-impact-checklist.md) 中对应的验收项或证据要求。
- 设备链路改动后，同步更新本页“设备服务链路”。
- 模板或 Flow 改动后，同步更新 [算法与模板](../algorithms/README.md)。
- 项目包业务字段改动后，同步更新 [项目说明](../../00-projects/README.md) 和对应项目页。
- 插件装载规则改动后，同步更新 [现有插件能力说明](../plugins/README.md)。
