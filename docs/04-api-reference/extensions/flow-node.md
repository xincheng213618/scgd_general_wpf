---
knowledge_id: "flow.node-extension"
knowledge_type: "guide"
status: "current"
summary: "说明服务与本地节点基类、请求与响应扩展点、分支输入隔离、属性编辑和流程完成的边界。"
aliases: ["FlowLocalExecution","CreateLocalExecution","如何新增Flow节点","CVCommonNode","CVBaseServerNode","LocalFlowNodeBase","CVStartCFC输入快照","getBaseEventData","CVEndNode"]
code_paths: ["Engine/ST.Library.UI/NodeEditor/STNodeAttribute.cs","Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs","Engine/FlowEngineLib/Base/FlowLocalExecution.cs","Engine/FlowEngineLib/Base/CVCommonNode.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/FlowEngineLib/Start/BaseStartNode.cs","Engine/FlowEngineLib/End/CVEndNode.cs","Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowNodeContextMenuService.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCleanupNode.cs","Engine/ColorVision.Engine/Services/Files/FolderCleanupService.cs","Engine/ColorVision.Engine/FlowProcessing/PreProcess/FolderSizePreProcessor.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility"]
test_paths: ["Test/ColorVision.UI.Tests/CompatibilityNodeMigrationTests.cs","Test/ColorVision.UI.Tests/FlowLocalizationTests.cs","Test/ColorVision.UI.Tests/LvCameraLocalForwardingTests.cs","Test/ColorVision.UI.Tests/ConventionalFlowNodeTests.cs","Test/ColorVision.UI.Tests/FlowRuntimeCompletionTests.cs","Test/ColorVision.UI.Tests/LocalCleanupNodeTests.cs","Test/ColorVision.UI.Tests/FolderSizePreProcessorTests.cs"]
related: ["platform.extensibility","flow.index","flow.runtime","ui.property-grid"]
---

# FlowEngineLib 节点扩展

Flow 节点建立在 `STNode` 和 `FlowEngineLib` 基类上。服务节点负责构建请求、接入 MQTT 执行链并处理响应；指定节点也可由宿主接管本地执行，沿用同一命令完成链。客户业务计算由对应服务或算法承担。

## 选择基类

| 基类 | 职责 | 源码位置 |
| --- | --- | --- |
| `CVCommonNode` | 节点公共属性、控件和节点事件 | `Engine/FlowEngineLib/Base/CVCommonNode.cs` |
| `CVDeviceNode` | 原有服务与控制节点的设备标识，保留其流程保存字段 | `Engine/FlowEngineLib/Base/CVDeviceNode.cs` |
| `CVBaseServerNode` | 输入输出、MQTT 请求、超时及响应处理 | `Engine/FlowEngineLib/Base/CVBaseServerNode.cs` |
| `LocalFlowNodeBase` | Engine 本地异步执行、多输入汇合及分支输入快照 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs` |
| `LocalDeviceFlowNodeBase` | 本地相机和校正节点的资源选择 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalDeviceFlowNodeBase.cs` |
| `BaseStartNode` | 创建 `CVStartCFC`、维护运行状态及启动动作 | `Engine/FlowEngineLib/Start/BaseStartNode.cs` |
| `CVEndNode` | 完成流程并发布终态 | `Engine/FlowEngineLib/End/CVEndNode.cs` |

`CVCommonNode` 提供 `NodeName`、`NodeType`、`NodeID`、`ZIndex`，以及 `nodeEvent`、`nodeRunEvent`、`nodeEndEvent`，不包含设备标识。`CVDeviceNode` 和 `LocalDeviceFlowNodeBase` 实现 `IFlowDeviceNode`，声明 `DeviceCode`；服务节点从 `CVDeviceNode` 继承。通用代码仅在节点实现该接口时读取设备标识，不再从 `CVCommonNode` 获取。参数编辑使用[PropertyGrid 契约](../ui-components/property-grid.md)，模板和量程编辑器见 `Engine/ColorVision.Engine/PropertyEditor/FlowTemplatePropertiesEditors.cs`。

配置面板只显示用户可配置的节点身份字段。`NodeID` 是流程持久化和运行关联使用的内部标识，保留保存兼容但不允许在属性面板修改；Engine 本地节点的 `NodeName` 由节点类型生成，仅用于运行诊断、结果名称和请求记录，同样不暴露为配置项。通用循环节点是例外：其 `NodeName` 以“循环名称”显示，`LoopNode` 与对应 `LoopNextNode` 必须填写完全相同的值以匹配循环状态；这两个控制节点不显示未参与循环逻辑的 `DeviceCode` 和 `ZIndex`。

开始和结束节点的配置面板仅显示 `Title`。MQTT 开始节点已有的服务器、端口，以及起止节点内部名称和设备标识仍按原格式加载、保存并参与运行；精简面板不改写这些值。旧开始节点保存的 `FlowTimeout` 字段在加载时忽略，重新保存不再写入；执行等待由运行宿主的显式超时和具体服务节点的响应超时负责。

新建流程使用 Start（`MQTTStartNode`）。旧“手动开始”（`ManualFlowStartNode`）保留类型及端口用于已有流程加载和运行，不提供新建入口；Start 在纯本地流程中可由运行命令直接启动，外部 MQTT 触发和远端服务仍遵循各自的连接就绪要求。

节点说明省略通用执行端口 `IN`、`OUT`、`OUT_START`，保留 `IN_IMG`、`IN_POI` 等具名数据入口；端口列表只显示方向和名称，不显示内部托管类型。此过滤仅用于说明，节点实际端口、连接和执行数据保持原样。

迁入 Engine、仍保留旧流程名称和保存标识的节点集中在 `Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/`，再按设备或功能分组。其中需要 Engine 模板或量程编辑器的相机、校准、POI、SMU、传感器和算法节点分别放在对应子目录，保留各自原 namespace、类名和保存标识；目录名不参与流程序列化。只有普通字符串、数值或枚举属性的节点，以及通用执行基类，继续由 FlowEngineLib 提供。迁移范围以属性编辑器依赖为准，无需整体搬迁节点库。

节点菜单按 `STNodeAttribute.Path` 的 `/` 或 `\` 分段递归构建，支持“算法 → OLED → 具体节点”三级菜单；KB 算法和图像转换直接归入“算法”分类。`CategoryOrder` 决定首级分类位置，`NodeOrder` 决定分类内的节点顺序，同序号或未指定时按名称排序；右键菜单和节点目录使用相同的节点顺序。菜单路径和排序可调整，既有节点的类型、序列化标识和执行协议保持独立。

Engine 内的模板与量程属性直接通过 `PropertyEditorType` 引用具体编辑器，声明放在属性定义上；例如 `BaseCameraNode` 声明的四个模板编辑器由 L/BV 节点继承。属性编辑不再经过类级名称映射或 Selector。校正模板依赖设备的刷新、增益联动和模板选择回写保持一致。公共基类的设备字段仍使用 `FlowDeviceNameEditor` 代理，让 FlowEngineLib 不引用 Engine 业务 UI。

本地节点中，只有相机取图和校正（含校正+实时 POI）保留“设备代码”，用于选择所需资源。十字定位、点阵畸变、发光区定位、FOV、POI、布点等纯算法节点，以及本地图片、文件景深融合节点，不声明、保存或在运行参数中携带 `DeviceCode`，也不自动选择算法设备或相机。旧流程中的多余字段按 STN 未知属性规则忽略，重新保存后不再写出；设备节点的字段和保存标识保持不变。

本地发光区定位、十字定位、十字 RGB 分离、点阵畸变、圆环鬼影、模板匹配和校正+实时 POI 统一使用上游图像，本地文件通过“加载图片”节点接入。旧流程中这些节点的 `ImageFilePath` 配置按 STN 未知属性忽略，重新保存后不再写出；原先仅靠该字段提供图片的流程需补接“加载图片”，否则缺少输入时失败。结果记录中的实际图像路径仍保留。文件景深融合保留多图文件夹/列表输入，算法参数模板与结果输出目录也各自保留。

持久化的纯算法与文件节点结果主表不填写设备代码，按批次、节点及主结果 ID 交接，通过 `ResultRoutes.LocalFlow` 发布进程内通知。结果仍可从流程批次或历史查询查看，不再自动推送到某个设备的结果窗口。“灯珠定位”是运行内存点集分支，不创建上述结果主从表，其下游本地 POI 测量也使用运行期句柄；不通过历史查询或服务端 MasterId 接入，详见[本地灯珠分析](../algorithms/local-native-analysis.md#flow-内存点集与本地测量)。FOV 可记录输入图像携带的相机来源；没有来源时保持空值，不猜选唯一配置的相机。此关联不参与 FOV 标量参数计算。

## 扩展服务节点

1. 继承 `CVBaseServerNode`，在构造函数中设置标题、`NodeType`、服务名、设备代码和 `operatorCode`。
2. 在 `OnCreate()` 中添加输入输出或编辑控件。
3. 重写 `getBaseEventData(CVStartCFC start)`，组装执行端需要的参数。
4. 按需要重写 `OnServerResponse(...)`、`Reset(...)` 或连接相关虚方法，处理响应与清理。
5. 核对 `GetSendTopic()`、`GetRecvTopic()`、`operatorCode` 和 `FlowServiceManager` 中的服务配置，并使用目标协议样例验证请求与响应。

`Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs` 是服务节点示例：它收集模板、颜色和图像路径等参数，生成发往算法服务的请求。`[STNode("...")]` 决定节点树分类，扩展时采用相邻节点的实际分组。

### 节点目录与右键菜单

流程画布的新增节点菜单直接复用 `STNodeTypeRegistry` 已缓存的反射结果；节点类型、分类元数据和各语言下的显示标题只解析一次，新程序集注册节点后再增量补入。`STNodeAttribute.Path` 只保存分类名称，使用 `/` 分隔多级分类；第一段分类通过 `CategoryOrder` 显式排序，同一顺序或未声明顺序的分类再按本地化标题自然排序。同组节点先按 `NodeOrder` 排序，同序号再按本地化标题自然排序；`SeparatorBefore` 在该项前已有菜单项时添加分隔线，不生成开头或重复分隔线。旧插件若仍声明 `00 全局`、`03_1 关注点` 等带编号路径，宿主会先提取编号作为兜底顺序，再将路径规范为 `全局`、`关注点` 等语义资源键进行本地化，因此不再保留带编号的资源文案；新代码应直接声明语义路径和 `CategoryOrder`。

“相机”分类依次显示校正、L/BV 相机、CV 相机、通用相机，分隔线之后依次为 AOI 定位相机（模板）、AOI 提取相机（模板）、AOI 定位相机（参数）、AOI 提取相机（参数）、AOI 定位+提取相机。两组定位/提取节点分别使用相机模板和直接采集参数；组合节点在一次取图中串联定位与提取。显示名称与排序不改变节点类型、保存标识、端口和服务请求，已保存画布中的自定义标题继续保留。

“本地设备”和“本地算法”是两个一级分类。本地设备包含相机、源表、加载图片、光谱与传感器采集，以及校正、布点、POI、实时 POI 和光谱参考校正；本地算法依次显示发光区定位、灯珠定位、FOV、点阵畸变、十字 RGB 分离、十字定位、MTF、模板匹配、圆环鬼影、景深融合、命令行脚本和缓存清理。这是菜单分组，不改变节点对设备资源的实际依赖。

“缓存清理”（`LocalCleanupNode`）可串接在流程任意位置，只有本地 `IN → OUT`，保留上游图像和结果身份。目录列表默认留空，需明确配置；支持多个目录、文件后缀、包含子目录、容量上限和目标容量。容量按各目录中匹配后缀且可读取的文件计算，空后缀匹配所有文件。默认上限 100 GiB、目标 50 GiB，仅在严格超过上限后，按最后写入时间从旧到新删除，直到不高于目标；目标允许为 0，但必须小于上限。

节点和预处理共用 `FolderCleanupService`，均拒绝相对目录、磁盘/共享根目录和链接/联接点，只删除指定目录内匹配的文件，保留目录结构。节点另保护当前内存帧的 CVRAW/CVCIE 文件和上游输入文件；扫描后大小或写入时间变化的文件会跳过。流程停止后不再启动后续删除。锁定或不可读文件记录失败并继续处理其余文件，目录扫描异常则让节点失败；受保护、变化中或删除失败的文件可能使容量无法降到目标。执行会永久删除文件，配置时应指定可清理的缓存目录，不能把数据库记录、标定资源或业务原件目录当作缓存。

清理统计写入本次节点结果，并向流程 `Data` 添加 `CleanupDeletedFiles`、`CleanupDeletedBytes`、`CleanupRemainingBytes` 和 `CleanupFailedFiles`；不覆盖 `MasterId`、`MasterValue` 或当前帧。预处理继续沿用原有后台执行、同实例串行和目标容量归一化规则；清理节点对无效容量直接报错。删除与停止边界由 `LocalCleanupNodeTests.cs` 的隔离临时目录测试验证，预处理语义由 `FolderSizePreProcessorTests.cs` 覆盖。

标记为 `Obsolete` 的节点不会出现在新增节点菜单或 Copilot 节点目录中，但类型及序列化身份仍保留，用于加载旧流程；本地“关注点布点(参数)”和“值显示HUB”遵循这一兼容规则，需要布点时使用原有关注点节点。

Engine 本地节点由 `LocalFlowNodeBase` 在输入到达时捕获 `CVStartCFC` 快照，再把快照副本交给异步执行；`CVBaseServerNode` 也在运行请求前复制动作，服务响应和宿主本地执行共用此副本。Start 的同一个动作可以扇出到多条并行分支，但各节点不得直接修改这份共享输入；单输入和多输入节点都通过各自的快照传递 `MasterId`、`MasterResultType` 与 `MasterValue`。运行资源仍由同一次流程共享并按其资源生命周期管理，分支结果字段则保存在独立的 `Data` 字典中。执行返回或抛出异常时，如果本次 `RuntimeResources` 已释放，本地基类丢弃迟到的完成，不再发布节点结束事件或继续传递输出；节点仍需自行取消在途计算并在文件、数据库等写入前检查有效性。

`FlowRepeatRegion` 接受单入口、单出口的无环子图，内部可分流至多输入节点，但所有分支须在框内汇合，内部输入不得从框外接入。每轮各流程输入与各节点输出仅接受一次，全部必需输入到达且内部节点完成后才推进，重复及迟到输出不参与下一轮；中间轮资源在换轮时回收，仅最终轮从出口放行。多输入节点使用 `CVStartCFC.IsSameExecutionIteration` 匹配：重复框内须同运行、同轮次并共享同一份运行资源，框外维持原有流水号匹配。服务 Hub 在锁内收齐并消费一组输入快照，再在锁外构建和发送本次请求，避免同步响应重入下一轮时清掉新输入。

`Timer` 是延时透传节点，可单独重复，也可与源表、光谱仪等节点一起放入重复框，每轮均执行配置的毫秒延时。它保留原动态端口数量、端口索引和保存身份；未连接的备用端口不参与重复区域判定，实际接入的非流程通道或多个独立流程通道仍不能纳入同一重复框。每次延时捕获自己的动作快照，运行结束释放资源或收到停止动作后不再放行该次延迟输出。相关重复、快照、停止和保存兼容边界由 `Test/ColorVision.UI.Tests/TimerRepeatTests.cs` 覆盖。

支持逐轮参数的节点实现 `IRepeatParameterNode`，通过 `RepeatParameterDefinition` 显式声明稳定参数标识、类型、默认值、单位、数值范围和不可用原因。只有声明过的参数可被重复框数组覆盖；数组存于节点的 `RepeatParameterValues`，随画布保存，整次运行开始时深复制并校验整个框。`ResolveRepeatParameter` 根据当前轮次解析值，框外或未配置数组时回退节点默认值。不可通过临时改写节点属性再恢复来实现覆盖。

`LocalFlowNodeBase.CaptureExecutionParameters` 在请求事件与异步任务之前创建一次参数快照；`BuildRunPayload` 与 `ExecuteLocal` 通过 `GetExecutionParameters<T>` 共用这份快照，准备失败走普通节点失败链。现有本地相机、源表和光谱节点采用该机制，避免日志、设备请求和结果保存分别读取不同轮次或被修改后的属性。新增节点需同时保护设备模式冲突、实际单位和流程资源生命周期，不能只开放属性列表。

单节点设备批量执行可调用 `TryGetSingleNodeRepeatParameters` 领取当前运行冻结数组的独立副本，只在首轮、单节点且至少两次时可用；领取绑定本次动作实例，副本或其它节点不能替它完成。设备确认全部测量和结果处理成功后，使用同一动作与冻结次数调用 `CompleteRepeatBatch`；正常输出路径将重复框推进到最终轮，仅放行一次。未使用批量能力时继续原有逐轮执行，失败/取消不能标记成功或重复启动测量。该能力不允许修改图连线、默认属性或保存的重复次数。

### 宿主接管服务节点的本地执行

`CVBaseServerNode.CreateLocalExecution(CVMQTTRequest)` 默认调用可选的宿主工厂 `FlowLocalExecution.CreateForNode`；未注册或返回 `null` 时保留 MQTT 路径。对应的 `CanExecuteLocally` 判断用于 `RequiresRemoteService`，使编辑器、无界面执行与断线 MQTT 开始节点按实际后端检查服务依赖。Engine 注册普通光谱/EQE 节点的本地转发，节点类型和序列化身份仍保留在 FlowEngineLib。指定节点也可重写方法返回宿主实现的 `FlowLocalExecution`；选择阶段抛异常时按本次命令失败处理，不回退到 MQTT。普通 `FlowEngineLib.LVCameraNode` 的实现在 Engine 内，直接调用本地相机执行器，返回 `null` 时继续原服务请求。它通过 `STNodeSerializationModelAttribute` 保留旧端可识别的保存标识；FlowEngineLib 只提供执行基类和宿主钩子，不引用 Engine。

- `Execute()` 在后台准备结果；此阶段不交接流程帧或写流程结果记录。
- `Complete(CVStartCFC)` 仅在响应成功领取原命令完成权后调用，负责落库、资源交接和生成普通响应数据。原消息 ID、超时、暂停响应缓存、失败策略和节点完成事件继续由基类管理。
- `Dispose()` 释放未被接受的结果，包括超时或停止后的晚到帧；已交给流程的资源由流程寿命管理。

本地分支在执行前复制 `CVStartCFC.Data`，避免结果字段串入并行分支，仍共享 `RuntimeResources`。不要通过修改下一次打开偏好改变执行中的后端。相机参数、保存和转发范围见[相机采集契约](../../01-user-guide/devices/camera.md)。

## 流程完成与节点完成

`BaseStartNode` 创建并保存 `CVStartCFC`，通过 `m_op_start` 和 `m_op_loop` 分发启动动作，管理 `Ready`、`Running` 及 `startActions`。`CVEndNode` 接收开始或循环动作；其 `DoNodeEnded(...)` 仅在 `TryDoFinishing()` 成功时调用 `FireFinished()`，避免重复发布流程完成。

`nodeEndEvent` 只表示单个节点结束。整条流程的完成需要到达结束节点并发布 `FireFinished()`；服务主题或操作码不匹配通常表现为超时或没有响应。运行会话的状态与失败语义见[FlowEngineLib](../engine-components/FlowEngineLib.md)。

## 验证

- `ConventionalFlowNodeTests.cs`：常规节点契约。
- `CompatibilityNodeMigrationTests.cs`：迁移前保存、排除退役节点后的合成画布，检查持久化字段、连线、请求、菜单分类及直接属性编辑器。
- `FlowRuntimeCompletionTests.cs`：流程终态。
- `LvCameraLocalForwardingTests.cs`：服务节点由宿主本地执行时的路由、结果交接、命令终态和资源释放。

测试均位于 `Test/ColorVision.UI.Tests/`。新增服务协议需要目标节点的请求、响应和资源生命周期用例；硬件执行应在获授权的测试环境中验证。
