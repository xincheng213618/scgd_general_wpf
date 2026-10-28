---
knowledge_id: "engine.devices"
knowledge_type: "topic"
status: "current"
summary: "设备工厂、资源重载、显示装配与详情视图按需初始化；旧对象释放、集合重建和显示替换并非一个事务，记录存在、界面可见、服务在线和动作完成分别判断。"
aliases: ["设备打不开","设备服务","设备连接","设备资源有记录却不出现","如何新增设备服务","设备资源重载","设备资源过滤","设备类型字典","组关联资源","空设备树","管理员服务配置","进入采集窗口","切换列表","LastSelectIndex","运行对象生命周期","显示集合","ServiceManager","t_scgd_sys_resource","t_scgd_sys_resource_group","DeviceServiceFactoryRegistry","ServiceTypes","LoadServices","LastGenControl","设备控制分组","CreateGroupCommand","设备行置顶","取消置顶","DisplayPinButton","设备详情按需初始化","ViewShell","EnsureInitialized","ViewCamera","ViewSpectrum","AlgorithmView","ViewCalibration","InspectDeviceServices","inspect_device_services","已加载设备目录","设备缓存状态","HeartbeatObservationKnown","UnknownDevices","心跳未观测"]
code_paths: ["Engine/ColorVision.Engine/Dao/SysResourceModel.cs","Engine/ColorVision.Engine/Dao/SysDictionaryModel.cs","Engine/ColorVision.Engine/Dao/SysResourceGoupModel.cs","Engine/ColorVision.Engine/Dao/VSysResourceDao.cs","UI/ColorVision.Database/BaseTableDao.cs","Engine/ColorVision.Engine/Services/LocalConfigurationDao.cs","Engine/ColorVision.Engine/Services/ServiceManager.cs","Engine/ColorVision.Engine/Services/ServiceInitializer.cs","Engine/ColorVision.Engine/Services/WindowService.xaml.cs","Engine/ColorVision.Engine/Services/WindowService.xaml","Engine/ColorVision.Engine/Services/DevicePropertyWindow.xaml","Engine/ColorVision.Engine/Services/DevicePropertyWindow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs","Engine/ColorVision.Engine/Services/Type/TypeService.cs","Engine/ColorVision.Engine/Services/DeviceService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Spectrum/DeviceSpectrum.cs","Engine/ColorVision.Engine/Services/Devices/Spectrum/DisplaySpectrum.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Spectrum/Views/ViewSpectrum.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DeviceAlgorithm.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithm.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/DeviceCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/DisplayCalibration.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/Views/ViewCalibration.xaml.cs","UI/ColorVision.UI/DisPlayManager.cs","UI/ColorVision.UI/DisplayPinButton.xaml","UI/ColorVision.UI/DisplayPinButton.xaml.cs","UI/ColorVision.UI/DisPlayControlPanel.cs","UI/ColorVision.UI/Docking/DockPanelTitleAction.cs","UI/ColorVision.UI/Views/DockViewManager.cs","UI/ColorVision.Solution/Workspace/DockViewManager.cs","Engine/ColorVision.Engine/Services/CopilotDeviceServicesReader.cs","ColorVision/Copilot/Capabilities/CopilotDeviceServicesInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotDeviceServicesInspectionService.Cursor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectDeviceServicesTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolEnvironment.DeviceServices.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.DeviceServices.cs","ColorVision/Copilot/Skills/colorvision-flow-diagnostics/references/checklist.md","Engine/ColorVision.Engine/Services/CopilotDeviceContextFactory.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotBusinessContextBuilder.cs"]
test_paths: ["Test/ColorVision.UI.Tests/OfflineDeviceConfigurationTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceServicesSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceHeartbeatSourceTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceHeartbeatContextTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceDiagnosisHeartbeatWorkflowTests.cs"]
related: ["engine.index","platform.runtime","operations.device-configuration","engine.mqtt","engine.rc-registration","engine.results","engine.spectrum-device","operations.camera","operations.motor","operations.smu","operations.calibration","operations.file-server","operations.flow-device","flow.session","ui.property-grid","ui.database"]
---

# Engine 设备资源与运行装配

`Services/` 中的设备服务不是通用 DI 服务。数据库资源经类型字典和工厂生成 `DeviceService`，设备列表、显示页、MQTT 通信和 Flow 再使用这些对象。**资源存在、设备被装配、显示页可见、服务在线、实际动作成功是不同状态。**

本页覆盖设备资源筛选、运行对象重建、显示装配与类型扩展。配置创建、导入、保存和删除见[设备资源配置与持久化](../../01-user-guide/devices/configuration.md)。

## 初始化与资源树

`ServiceInitializer.Order=5`；启动时物理相机、服务层级、待应用服务更新分别调度到 UI；设备卡片在同一次 UI 操作内创建，全部完成后一次性替换显示集合；启动与手工刷新共用同步入口，避免卡片构造之间的重复调度增加等待。详情按需初始化契约见下文。无论 MySQL 是否连接，都会初始化物理相机管理器、设备集合和显示控件（包括流程面板）。连接时沿用 MySQL 资源树与原有原生资源初始化；未连接时读取本地配置，以 `ServiceTypes` 建立类型列表。`ServiceManager` 构造器通过 UI Dispatcher 调用 `LoadServices()`，并订阅后续连接变化；创建单例不是无副作用的只读查询。

本地资源和许可证复用现有字段及窗口，写入 `%APPDATA%/ColorVision/Config/ColorVision.Local.db` 的配置文档。资源 ID 使用小于等于 -2 的本地身份，父子及组引用也使用本地 ID；已打开的本地对象恢复联网后仍保存到本地。MySQL 与本地配置独立，不复制服务器资源、不自动同步，也不修改服务端表结构。结果、图像、运行记录不写入这个配置库。

早到 RC 更新只在管理器实例不存在时暂存，每类保留最新引用；应用顺序与异常边界见[RC 服务快照](./rc-registration.md)。缓冲锁不代表远端一致快照，实例已存在也不代表本地资源已匹配。

`LoadServiceResourceSnapshot` 查询资源并建立本轮内存索引，`LoadServices` 按下列关系装配：

| 层 | 来源与过滤 |
| --- | --- |
| `TypeService` | `SysDictionaryModel` 的 `Pid=1` 字典项，`Value` 对应 `ServiceTypes`；字典查询不检查启用、删除、隐藏或租户字段，装配时再排除下文列出的类型 |
| `TerminalService` | 相同 Type 的根资源：`Pid=null`、`TenantId=0`、`IsDelete=false`；这一层没有检查 `IsEnable` |
| `DeviceService` | 终端的直接子资源：`IsEnable=true`、`IsDelete=false`、`TenantId=0`，按子资源自己的 Type 查工厂 |
| 设备子资源 | 按设备 ID 找直接子资源，要求启用且未删除，不检查 `TenantId`；`Group` 生成组，Type 30–50 生成校准资源，其余生成 `ServiceFileBase` |
| 组关联资源 | `SysResourceGoupModel.GroupId` / `ResourceId` 关联；目标存在即载入，不检查目标的启用、删除、租户或父级字段；普通关联生成 `ServiceBase`，组和校准资源继续按 Type 处理 |

组关联目标不存在时跳过；同一目标的重复关联不会自动去重。这些过滤条件不构成统一的租户或禁用隔离规则，不能把设备层的限制自动套到字典、子资源和组关联。本轮重载使用的私有 `LoadGroupResource` 遇到重复祖先 ID 时记录警告并停止继续递归；父调用仍会加入已创建的组节点，因此不是完全排除重复 ID 节点。另一个公开入口 `LoadgroupResource` 递归查库但没有此祖先集合，不能套用同一循环保护保证。

## 重载、旧对象与集合引用

`LoadServices()` 是重新装配，不是按 Code 原地刷新旧设备：清理后会通过工厂构造新的运行实例；即使 Code 相同，旧窗口或调用者持有的对象引用也不会被这段代码自动改指向新实例。

| 阶段 | 当前边界 |
| --- | --- |
| 清理旧对象 | 先清理设备 Copilot 上下文/映射，逐个调用当前 `DeviceServices` 的 `Dispose()`；某个 Dispose 失败会记录警告并继续，不保证该对象已释放 |
| 清理上次生成集合 | 接着执行 `LastGenControl?.Clear()`，发生在字典与资源查询之前。它不是独立快照：`GenDeviceDisplayControl()` 最终使它引用 `DeviceServices`；`GenControl(collection)` 则直接保存调用者传入的集合，因此也可能清空调用者持有的集合 |
| 查询与重建 | 再查询字典及本轮资源索引，依次清空/构造类型、终端、设备与子资源，最后发布 `ServiceChanged`。这些查询不是数据库事务快照，构造和通知也没有整轮回滚 |

字典查询与资源查询的失败行为不同：`GetAllByPid(1)` 在连接标志为 false 或捕获查询异常时返回空列表；若后续资源查询成功，仍会重建空类型/设备集合并发布 `ServiceChanged`。所以“完成加载”日志或没有抛出异常不能证明字典查询成功，应同时查 DAO 错误日志。通用 DAO 语义见[数据库访问](../ui-components/ColorVision.Database.md)。

`LoadServiceResourceSnapshot` 则直接执行数据库查询，没有本地捕获；此处抛出时，旧对象可能已被 Dispose，设备集合可能因共享引用已被清空，但旧类型树或显示项仍在；更晚失败也可能留下部分新集合。不能把“加载失败”解释为旧运行状态完整保留。具体设备是否释放全部句柄和事件取决于其 Dispose 实现；删除时的清理限制集中在[设备配置契约](../../01-user-guide/devices/configuration.md#导入、导出与删除)。

构造器始终首次重载，离线时从本地配置装配；`MySqlConnectChanged` 订阅同样调用 `LoadServices()`。断开会切换到本地资源，不能认为断开通知天然是无操作，也不要在只读诊断中用切换数据库连接或重载来试探；这些动作可能影响运行设备和旧窗口。

## 工厂存在不等于默认可见

`DeviceServiceFactoryRegistry.RegisterDefaults()` 是内置注册的来源，不是扫描所有 `Device*` 类自动发现。它注册 Camera、PG、Spectrum、SMU、Sensor、Algorithm、FilterWheel、Calibration、Motor 和 LightingControl。

默认类型树明确过滤 **FileServer、FocusRing、Flow、ThirdPartyAlgorithms、ThirdPartyAlgorithms32、PowerControl**；LightingControl（值 16）没有被该过滤排除。过滤发生在类型节点层，装配可见终端的子资源时仍按子资源自己的 Type 查工厂，不能将类型过滤说成全局禁止实例化。

遗留子资源的 Type 为 6 时，没有对应的内置工厂，默认返回 null，跳过创建而不删除资源记录。`ServiceTypes.FileServer = 6`、RC 对应协议值和设备文件保存配置的用途见[旧 FileServer 资源与文件保存配置](../../01-user-guide/devices/file-server.md)。

遗留子资源的 Type 为 12、13 或 14 时，没有对应的内置工厂，默认返回 null，跳过创建而不删除资源记录。协议值与旧流程节点读取的兼容用途见[旧 Flow 与第三方算法资源兼容](../../01-user-guide/devices/flow-device.md)。

`CreateService(resource)` 在工厂未注册时返回 `null`，该资源不会进入运行集合；工厂构造抛异常则会向外传播，不是同一种“跳过”行为。重复注册默认抛错，明确 `replace=true` 才替换既有工厂，不能无意覆盖其它模块的类型所有者。

## 设备树与显示区

1. 打开 **工具 → 管理员服务配置**（`WindowService`）。列表模式由 `ShowType2` 配置决定，初始值为设备；**切换列表** 按设备 → 类型 → 终端循环。
2. 选择设备查看 `GetDeviceInfo()`；选择终端或类型节点查看 `GenDeviceControl()`。这里是信息/配置页，不是主界面的设备控制页。
3. 此窗口是纯配置页，不提供采集入口或独立设置齿轮。关闭时比较打开时与当前资源树的设备/终端配置、实例身份和顺序；有变化才重新生成主显示区，无变化直接关闭。切换列表、选择节点和在线心跳不属于配置变更。各配置编辑器保留原有保存语义，窗口关闭不会统一保存或重启所有设备；显示更新失败时保留窗口并显示错误。

选择设备时，窗口发布该设备的 Copilot 业务上下文；窗口重新激活时会再次发布仍选中的设备，避免用户从其他业务页面返回后继续使用后一个页面的 Live Context。重新激活时没有设备选择则清除本窗口此前发布的来源；关闭窗口后清理设备来源，并重新发布主显示区当前上下文。此刷新只改变 Copilot 上下文，不连接、重启或操作设备。

自动设备上下文将心跳观测分为已观测 Online、已观测 Offline 和 Unknown。`DeviceService.IsAlive` 继续保持公开 `bool` 签名和默认 `false`；基类 setter 的显式 true／false 赋值记录观测，内部 `ObservedIsAlive` 为未观测保留 `null`，Factory 的 `HeartbeatObservation` 据此投影。未赋值时 Copilot 的单设备状态、runtime 字段及诊断上下文保持 Unknown，不能将默认 false 当作离线。时间戳可以作为原记录显示，但不产生心跳观测；MQTT 缓存 `OperationalStatus` 独立呈现，不转换为心跳状态，也不从 RC 终端时间合成设备存活或引入新的时间阈值。

公共 `CopilotDeviceHealthContextSnapshot` 默认新行的 `HeartbeatObservationKnown=false`；显式初始化 `IsAlive=true` 或 `false` 沿用旧的已观测语义，显式 `HeartbeatObservationKnown=false` 则优先，且不依赖初始化或 JSON 属性顺序。Factory 先对全部设备计算已知在线、已知离线及 `UnknownDevices`，再限制最多60条展示行；只有未知数大于0时才增加 Unknown 计数，纯已知场景保留原文本。这个自动上下文的观测投影不改变正式查询只读取缓存状态的范围。

正式只读查询使用单个共享工具 `InspectDeviceServices`（Agent）／`inspect_device_services`（本机 MCP），提供已有运行对象的目录分页或精确对象 focus。旧自动上下文的单设备意图推断、当前选择回退及60条展示上限保留；正式工具不按名称、Code、资源 ID 或当前选择猜测对象。`CopilotDeviceServicesReader.CaptureCurrentAsync` 在排队前冻结已有 `ServiceManager` owner，UI 回调采集前后核对同一 Current、集合和成员并处理取消，首次查询也不能排队后改采另一宿主。App 默认来源直接委托这个异步入口，不创建管理器、不重载资源、不查配置或数据库、不创建详情／显示控件，也不主动探测、连接、重连或控制设备。

省略 `device_ref` 时读取目录，`max_devices` 默认40、范围1–100；可选非空 opaque `cursor` 最多4,096字符，须原样续传，页大小可变。focus 须同时传目录返回的32位十六进制 `device_ref` 和 `source_instance_id`（作为 `expected_source_instance_id`），且不能带 cursor；目录请求也可用 expected 字段锁定宿主。两种 nonce 分别绑定对象实例和 manager 实例，不是资源编号、Code、名称或哈希，也不是跨重载的持久身份；本地负资源 ID 只作为 metadata 保留。对象移除或替换后旧引用不回退匹配其他对象，须重新发现；同一宿主内仍存在的精确对象可继续 focus。

成功正文为最多18,000字符的完整 JSON，`source=current_loaded_device_registry`，包含 `source_instance_id`、成员对象集合的 `inventory_revision`、`observed_at_utc`、`total_devices`、`matched_devices`、`devices`、`has_more` 和 `next_cursor`。目录按 device ref 的 Ordinal 升序排列；游标绑定宿主、成员对象 revision 与实际交付末行，当前成员对象集合或宿主与游标绑定不符时需重新查询。名称、资源 metadata 和缓存状态变化不冻结在游标中。正文预算只缩减未交付尾行，不跳过记录；无法容纳单行时明确失败且不推进。

每行交付 `device_ref`、旧上下文提示 `context_source_id`、资源 ID、Code、名称、服务类型及 `status_code`／`status`／`status_recognized`／`status_read_state`；上下文 source 不是正式对象引用。Code／名称各最多256字符并脱敏，资源 metadata 直接来自当前对象关联的 `SysResourceModel`。状态只读取经过审查的 Engine 内置精确类型及既有服务缓存，不调用插件的虚拟状态 getter；插件仅交付 metadata，状态为 `unsupported`。读取状态还区分 `cached`、`no_existing_service` 和 `read_failed`；原始状态码保留，recognized 仅表示能否识别枚举。工具不使用 `IsAlive` 或 `LastAliveTime` 推导在线／离线，采集时间也不是最后心跳或缓存更新时间。缓存状态不证明设备健康、就绪、动作完成或测量结果。

`coverage` 说明 `returned_devices`、`text_truncated`／`text_redacted`、`output_limited`、本页 `unknown_status_devices` 和 `all_matched_devices_returned`；后者只比较本次响应与全部匹配对象，最后一个续页也不汇总前页。当前 manager 不存在时，首次未绑定查询返回 `NotFound`；已绑定来源失效、过期游标或排队期间 owner 改变为 `Validation`，有效来源内不存在的精确对象为 `NotFound`，不伪装成空目录。有来源且集合确为空时可成功返回空页，但不证明数据库无配置或没有未注册设备。目录读完也不证明设备整体健康。

`CopilotDeviceServicesSourceTests` 保护 UI 源采集、排队前 owner 绑定、对象身份与不触发虚拟插件读取；`CopilotDeviceServicesServiceTests` 保护预算、游标／focus、未知状态与明确失败；`CopilotDeviceServicesToolTests` 保护 Agent／MCP 同协议；`CopilotDeviceServicesWorkflowTests` 覆盖超过自动摘要容量后的发现、精确 focus 和成员／宿主替换。引用测试不表示真实设备连通、缓存新鲜度或现场验收。

自动心跳投影由 `CopilotDeviceHeartbeatSourceTests` 保护未赋值与显式 false、时间戳／缓存状态独立及全 fleet 计数，再由 `CopilotDeviceHeartbeatContextTests` 保护默认行、旧显式布尔值、Unknown 优先的初始化／JSON 兼容和展示限制。`CopilotDeviceDiagnosisHeartbeatWorkflowTests` 通过真实 `AskCopilotCommand` 验证附加 context 的 Unknown／Offline／Online 与独立缓存状态；这些测试不建立设备心跳来源或存活阈值。

设备卡片或上下文菜单中的 **属性** 同样复用 `GetDeviceInfo()`，但由 `DevicePropertyWindow` 提供独立窗口外壳：窗口显示设备名称与 Code，并在带边框、圆角和内边距的内容区承载设备页。这个外壳只属于独立窗口；管理员服务配置和终端详情继续使用各自已有的面板边界，不把窗口留白重复写进设备控件。

滤色轮和光谱仪的主界面设备卡片在连接/状态行提供与 SMU 相同的配置齿轮。展开的卡片在未连接、离线或未授权时仍显示入口，点击直接执行该设备的 `EditCommand`，进入原有配置窗口；可执行性仍由原命令的管理员权限判断控制，确认后的保存沿用原设备逻辑。

滤色轮配置窗口提供 **使用本地滤色轮**。沿用本地源表的双后端方式，此开关保存在当前电脑的 `DisplayCfwPortConfig.UseLocalCfwPort`，不写入服务配置；旧配置默认继续服务模式。配置窗口使用独立草稿，取消不改变连接参数或本地偏好。先关闭当前连接，再勾选本地模式并确认，按实际设备设置串口、波特率和 6/12 孔位数量，点击 **连接**。启用自动连接时，本地卡片首次加载会尝试打开一次；原有 `FWNode` 的独立滤色轮类别在本地模式下也会按需打开。相机内置滤轮保留相机路径；**光谱仪+ND** 类别的本地转发、标定文件与校零契约见[主程序光谱仪](./spectrum-device.md)。

本地滤色轮通过 `NdCFWPortAPI` 调用随应用交付的 x64 `cvCamera.dll`，支持独立串口打开、关闭、切位和读位，不经 MQTT。孔位仍为从零开始的整数；切位后必须读回目标孔位才成功，`RetryCount` 表示首次失败后的额外尝试次数，`Delay` 在读回前等待。`Timeout` 传给 SDK 打开接口，SDK 内部切位、读位另有固定等待，不能将该值或取消请求解释为硬件停止的硬截止。未绑定物理相机时保留默认六孔名称，12 孔额外显示位置 6–11；已绑定相机的自定义孔位映射继续优先。`IsCVMode`（相机内置滤轮）和 `EnableVSPHub`（虚拟串口集线器）不能用于本地独立连接，打开前明确报错；这两种配置继续使用对应相机或服务功能。

本地手动控制、Flow 和本地光谱校零共享设备会话。命令串行执行，关闭和释放等待在途 SDK 调用返回；取消不强行释放句柄。释放失败保留串口占用标记并拒绝重新连接，需重启程序处理。同一程序内同串口的其它滤色轮以及已知的服务连接受互斥保护，服务离线不等于已关闭；模式偏好修改后，已打开的连接仍由原后端关闭。其它进程和相机直接占用串口不在该本地门禁内，连接失败应核对现场服务占用。光谱校零继续使用原占用及孔位确认机制，并等待本地 SDK 完成后才释放占用，不套用 MQTT 的回执超时。实现和模拟回归分别见 `Engine/ColorVision.Engine/Services/Devices/CfwPort/Local/`、`Test/ColorVision.UI.Tests/LocalCfwPortTests.cs`；模拟验证不代替现场 6/12 孔滤轮、接线及实际到位验收。

内置设备卡片标题与 Flow 设备选择器的属性入口统一使用主题 `ButtonProperty` 样式：24 DIP 点击区域、单色信息图标和属性工具提示。按钮不显示边框或默认焦点虚线框，悬停与键盘焦点通过图标主题强调色反馈。图标随深浅主题更新，只表示打开属性窗口，不承担在线状态显示；不叠加常亮的绿色状态灯。它仍绑定原设备的 `PropertyCommand`，与相邻的置顶按钮各自独立。设备与工作流程内容区未选中时使用 `CV.Border.Weak` 的低对比度主题描边，选中时保留深浅主题各自的紫色高亮。

`GenDeviceDisplayControl()` 沿当前类型树生成主显示区；`GenControl(collection)` 使用指定设备集合。两者都先加入共享 `DisplayFlow`，只有设备 `GetDisplayControl()` 返回 `IDisPlayControl` 才追加该页，最后通过 `DisPlayManager.ReplaceControls` 替换显示集合。

`LoadServices()` 最后发布 `ServiceChanged`，但本身不调用 `GenDeviceDisplayControl()` 或 `ReplaceControls()`；释放设备、清空 `LastGenControl` 也不等于替换主显示集合。初始化器会另行生成显示区；配置窗口在 `OnClosing()` 检测到变化时生成显示区，`OnClosed()` 负责上下文清理。因此资源集合、主显示项和旧窗口引用可能处于不同轮次；新增资源后列表出现、主区域出现、Flow 能按正确 Code 绑定，应分别核对，不能只检查一个窗口。

`ReplaceControls` 清空并重新填充显示集合，恢复分组、顺序、置顶、隐藏和开合状态后，优先按稳定 `PersistenceKey` 恢复选中对象，旧配置才回退到 `LastSelectIndex`；索引越界或原对象已隐藏时选择第一个可见项。内置设备使用 `Config.Code` 作为稳定键，工作流程使用固定 `Flow` 键；第三方实现不提供该属性时仍回退到 `DisPlayName`。这只替换显示集合，不调用旧控件的 Dispose。

显示控件装入设备控制面板时保留各自左右外边距，将统一的 2 DIP 项间距全部放在控件下方，上方不再额外留白。分组标题和标题拖动能力继续保留；滚动内容底部不再创建“分组 / + 分组”页脚。主程序和 Engine 独立宿主都使用实现 `IDockPanelTitleActionProvider` 的 `DisPlayControlPanel`，标题栏的“+”由 `DisPlayManager.ManageControlsCommand` 打开统一管理窗口。该派生宿主显式解析原 `ScrollViewer` 的隐式主题，避免扩展标题动作后回退成系统默认的粗滚动条。各设备提供的 `IDisPlayControl` 无需实现标题命令，也不会多出自己的标题按钮。

相机的打开与视频入口按内容宽度靠左排列，设置入口位于右侧。相机、滤光轮、光谱连接区和传感器内容区使用对称的上下留白；连接行后的可选参数块在自身上方提供行间距，隐藏参数块时不在连接行底部保留额外空白。

### 设备控制管理、隐藏与开合

“设备控制管理”窗口统一管理分组的新建、重命名、删除和顺序，也可调整每个控制项的分组、顺序、置顶、展开和显示状态。“显示”开关同时控制左侧设备控制项和关联的右侧详情页：隐藏时关闭详情页，布局恢复和启动显示全部视图时也跳过隐藏页；重新显示时恢复对应页面，不主动抢占当前页面焦点。隐藏不删除 `IDisPlayControl`，不停止设备服务、连接或流程执行。重新显示控制项时保留原分组、基础顺序、置顶和开合状态。全部控制项都隐藏时管理窗口仍列出这些项，作为恢复入口。

分组标题的“+”新建分组，每行的“…”或右键菜单提供重命名、上下移动和删除；默认分组不能移动或删除。右侧始终列出全部控制项，通过行内分组选择、显示开关、置顶和展开图标调整状态，行尾箭头调整所属分组内的基础顺序。行内操作直接作用于所在项，无需预先选中。

`DisPlayManagerConfig.HiddenControls` 和 `ControlExpandedStates` 与 `StoreIndex`、`PinnedControls`、`ControlGroups` 一样按 `PersistenceKey` 保存。标题开合、分组开合和管理窗口变更经过短延迟合写入配置，窗口关闭时刷新尚未写入的变更。展开状态只由 `ControlExpandedStates` 管理，没有记录的控件默认展开；旧的 `DisPlayName` 排序、分组、置顶、隐藏、开合和选择键会在遇到同一控件时复制到稳定键，兼容现有统一面板配置。

### 设备行置顶与顺序

工作流程和内置设备行使用统一的 `DisplayPinButton`：图钉独占标题右侧的 24 DIP 按钮区域，位于原有属性/设置入口左侧；悬停设备行或键盘焦点进入设备行时显示，已置顶时常显，提示切换为“取消置顶”。透明时仍保留布局空间，因此标题和设置图标不会随悬停跳动。图钉支持鼠标和键盘激活，点击不切换展开状态、不启动标题拖动，也不通过双击打开详情。

置顶在所属分组内生效；未建立分组时就是整个设备列表的顶部。多项置顶保持原有相对顺序，可继续拖动调整，跨组拖动保留置顶状态。置顶不改变设备配置、连接或流程执行顺序。

`DisPlayManagerConfig.PinnedControls` 按稳定 `PersistenceKey` 独立保存置顶状态；旧配置缺少该字段时全部未置顶。`StoreIndex` 保留基础顺序，置顶和取消置顶不重写它；重建显示集合时也按基础顺序规范化索引，避免把置顶顺序固化。拖动按同一置顶状态的相邻项转换回基础顺序，所以取消置顶后回到基础顺序（包含用户主动拖动的调整）。置顶或拖动重排现有集合时保持当前选中对象，并同步索引和稳定选择键。

真实窗口的触控、多 DPI 和现场设备操作仍需单独验收。

### 设备详情视图按需初始化

相机、光谱、算法、校准和 SMU 设备保留完整的真实设备卡片；设备区仍能同时显示多个展开面板，`IsSelected` 继续表示选择和上下文。按需初始化只作用于对应的 `ViewCamera`、`ViewSpectrum`、`AlgorithmView`、`ViewCalibration`、`ViewSMU` 详情内容，不减少 `IDisPlayControls`，也不延迟设备服务或硬件初始化。

| 入口 | 详情初始化与兼容契约 |
| --- | --- |
| 设备卡片登记 | `AddViewConfig` 登记 `Device.ViewRegistration` 的工厂与稳定设备身份，不立即创建详情。注册在设备构造时就按设备 Code／旧名称读取隐藏配置，布局恢复和启动显示全部视图时跳过隐藏项 |
| 公开访问 | `Device.View` 先执行 `EnsureInitialized` 再返回；原公开详情构造函数也立即初始化。因此直接访问图像、列表和结果操作的既有调用方仍取得可用控件 |
| 显示详情 | `Loaded` 仅在 `IsVisible` 为 true 时初始化；`IsVisibleChanged` 仅在 `IsLoaded && IsVisible` 时初始化。未选中的 Dock 标签也可能先收到 `Loaded`，不能只用 Loaded 判断用户正在查看详情 |
| 切换标签 | `EnsureInitialized` 防止重入及重复初始化；初始化后解除两种显示事件订阅，普通标签切换复用已有内容 |
| 隐藏与重新显示 | `DockViewRegistration.IsVisible` 表示是否允许设备界面出现，与控件当前是否可见分开。隐藏释放当前详情，重新显示创建新页面并补回标签，不主动激活；不回放关闭前的内存图像或列表，历史记录仍可查询 |

`ViewShell` 通过注册对象先切换到 UI Dispatcher，再取得或创建当前详情，避免后台线程持有构造锁等待 UI。详情控件和内部图形内容始终在 UI 线程创建，不在 `Task.Run` 中构造后跨线程挂载。空详情控件可能在进入 Dock 树时已触发 WPF `Initialized`，因此 `EnsureInitialized` 在 `InitializeComponent` 后显式调用原初始化处理函数，由该处理函数的状态保护避免执行两次。显式访问公开 `Device.View` 仍可构造控件，但显示管理器不会把隐藏设备的控件挂回文档；自动结果入口不使用此路径重建隐藏页。

相机、算法和校准详情在轻量构造时订阅原有 MQTT 回包及 `ResultMessageBus`，无设备参数的 `AlgorithmView` 不建立这两种订阅。消息沿各自原有设备、路由、结果类型及结果 ID 规则过滤，再回到 UI Dispatcher 更新尚存在且允许显示的页面；普通未激活标签仍可接收结果，管理窗口隐藏或关闭页面后不再追加和绘图。光谱、SMU 的 `PresentResult` 只向当前页面呈现，隐藏时连结果界面模型也不构造；排队回调复核页面身份，不能把已关闭的旧页面复活。设备服务的 MQTT 通信、业务状态、结果持久化和流程执行保持原路径；各结果的过滤和失败含义仍由设备主题及[结果交接链](./result-handoff-chain.md)维护。

这五类详情释放时解除自身的显示、配置、主题及结果订阅，清理其图像或图表、驻留结果和配置中可能保留页面的列引用；未初始化内容安全跳过。后续显示或排队回调不会重新初始化已释放详情。兼容的旧 `UserControl` 注册路径只撤下文档，实例仍由原 owner 管理；尤其工作流程页与运行图共享对象，隐藏不销毁流程图、不取消正在执行的流程。此边界不代表设备所有资源或第三方页面都有同等释放保证。

## 各模块的独立契约

| 问题 | 所属实现 | 主题 |
| --- | --- | --- |
| 通用传感器、本地 TCP/串口和指令模板 | `Services/Devices/Sensor/` | [本地通用传感器](./generic-sensor.md) |
| 相机服务、取图与运行参数 | `Services/Devices/Camera/` | [相机服务](../../01-user-guide/devices/camera.md) |
| 物理相机、许可、校准配置 | `Services/PhyCameras/` | [物理相机](../../01-user-guide/devices/camera-management.md)、[相机配置](../../01-user-guide/devices/camera-configuration.md) |
| 运动及位置状态 | `Services/Devices/Motor/` | [电机](../../01-user-guide/devices/motor.md) |
| 电压/电流与扫描输出 | `Services/Devices/SMU/` | [SMU](../../01-user-guide/devices/smu.md) |
| 本地校正与服务校准 | `Services/Devices/Calibration/` | [校准](../../01-user-guide/devices/calibration.md) |
| 旧 FileServer 资源装配与设备文件保存配置 | `Services/Devices/DeviceServiceFactory.cs`、`Services/Cache/FileServerCfg.cs` | [旧资源与文件配置](../../01-user-guide/devices/file-server.md) |
| 旧 Flow / 第三方算法资源的装配与读取兼容 | `Services/Devices/DeviceServiceFactory.cs` | [旧资源兼容](../../01-user-guide/devices/flow-device.md) |

PG、Spectrum、Sensor 等设备从 `RegisterDefaults` 定位具体配置、命令和显示实现；插件同名不等于同一个设备对象。MQTT 关联、返回与超时由[消息契约](../../02-developer-guide/engine-development/mqtt.md)维护，Flow 业务完成由[执行会话](../../01-user-guide/workflow/execution.md)维护。

PG 的“修改配置”打开独立单页 `EditPG`，分为设备、连接和按类型显示的设备参数。`PGConfigurationDraft` 编辑 `ConfigPG` 和 `DisplayPGConfig` 的副本，并分别保留网口地址/端口、串口名/波特率以及 `CH431.I2C` 设备索引/地址的临时输入；切换连接方式或类型不覆盖其它模式输入，打开已有 I²C 配置不重置其索引或地址。网口支持 IP 与主机名；串口可手填，本地模式填写本机端口，服务模式填写服务电脑端口。I²C 显示寄存器地址与通信速率，`GECS.V2.4` 编辑独立的 `Channel`，默认 `01`，保持与 PG 服务的字段和默认值一致；`RegisterAddress` 继续只表示 I²C 寄存器地址，其他类型隐藏这两项。

确定前验证当前可见输入的数字格式和所选连接参数：网口端口为 1～65535，串口波特率大于 0，I²C 索引为非负整数、设备地址为 0～255。失败或取消不修改当前设备配置；成功才把所选连接写回原有 `Addr` / `Port`，保留配置对象、订阅者及其余协议字段，由 `DevicePG.EditCommand` 沿设备原 `Save()` 链路保存。各模式的临时输入不进入资源数据库；打开窗口、切换模式和校验不操作真实设备，确定保存后服务器资源仍沿原链路发送服务重载请求，本地资源跳过该请求。`PGConfigurationDraftTests` 保护取消隔离、失败不写回、模式输入保留和既有序列化边界；真实服务与设备连通仍需现场验证。

“使用本地 PG”默认关闭，缺少该字段的旧配置继续使用服务。开启后，手动打开、关闭和控制命令以及旧 `PG`、`PG.For`、`PG.GECS`、`PG.GECS.Key`、`PG.GECS.Demura` 节点转到设备共享的本地 TCP/串口会话，不修改旧节点类型、指令字段或流程文件。流程未连接时自动打开并复用连接；手动控制仍先打开设备。连接超时默认 3000 ms，指令超时默认 10000 ms，均保存在本机 `DisplayPGConfig`，不写入资源数据库；流程仍保留节点最大超时及停止取消。修改模式、地址、类型或通道后，已打开的本地连接继续使用打开时的参数，关闭后重新打开才采用新配置。

`GECS.V2.4` 使用 `02 FF`、四位 ASCII 十六进制正文长度、ASCII 正文和 `03` 的帧格式；通道取 `Channel`，保留字符串形式及前导零；缺少该字段的旧配置使用服务默认值 `01`。内置上下电、上一图/下一图、指定编号、八种按键、Demura 擦除/写入/开关、DieId 和 OSC_TRIM 指令，校验完整帧、通道、命令与成功回包；半包继续等待，`ALIVE` 和当前命令的 `processing` 不算完成。电压电流校验并解析九路电压及九路电流，结果保留 `CmdType=POWER.MEASURE` 和 JSON 字符串 `ResultData`，其中为 `Channel`、`Voltages`、`Currents`。不自动重发可能产生写入副作用的 GECS 指令；已配置的 PG 模板覆盖命令沿用模板的超时、延时、重试及回包判定。GECS 没有内置 `CM_ReSetPG` 协议映射，需配置已确认的指令模板或使用独立 `KeyReset`，不自动将二者等同。

普通文本 PG 使用现有 PG 类型指令表，编号切图替换 `{0}`；沿服务文本模式的仅发送契约，写入成功不代表设备完成动作。未配置的命令明确失败，`CH431.I2C` 本地连接也明确拒绝，继续通过服务使用。本地 PG 与本地通用传感器共用通信会话基础和端点互斥，已知的服务占用必须先关闭，服务离线不代表释放。关闭或流程停止取消在途请求，超时、断线、畸形或不匹配回包关闭本地连接，防止残留数据进入下一条指令；后续执行可重新连接。实现入口为 `Services/Devices/PG/DevicePG.Local.cs`、`Services/Devices/PG/Local/`，`Test/ColorVision.UI.Tests/LocalPGTests.cs` 保护旧节点本地转发、协议、结果结构、取消与跨 PG/传感器占用边界；回环 TCP 和模拟检查不代替现场设备、串口或烧录验收。

## 扩展一个设备类型

1. 先核对已有 `ServiceTypes` 和资源语义，保持历史编号兼容；确有新类型才新增枚举及对应字典配置。加枚举不会自动生成数据库字典。
2. 在 `Services/Devices/<Module>/` 定义可兼容旧 JSON 的 `Config* : DeviceServiceConfig` 和 `Device* : DeviceService<Config*>`。通用配置加载/保存规则只在[配置契约](../../01-user-guide/devices/configuration.md)维护。
3. 用 `DeviceServiceFactory<TConfig>` 注册构造函数，必要时设置终端图标或 `configureConfig`。创建上下文传入 Code、Name 和终端主题，不能让新设备丢失通信身份。
4. 按实际需要实现 `GetDeviceInfo()`、`GetDisplayControl()` 和 `GetMQTTService()`；通常由 `MQTTDeviceService<TConfig>` 子类封装命令。基类 `GetDeviceInfo()` 抛 `NotImplementedException`，`GetDisplayControl()` 返回未实现 `IDisPlayControl` 的空控件，`GetMQTTService()` 返回 null；需明确实现需要的界面和通信能力。
5. 普通参数复用 [PropertyGrid](../ui-components/property-grid.md) 元数据；专用 Flow 补充面板归 `FlowProcessing/Editor/NodeConfiguration/`。客户判定、MES 和项目导出仍归项目包。
6. 在所属设备主题同步操作、配置、失败条件和源码/测试关联；通用装配与持久化规则通过链接复用。

## 失败定位与验证缺口

| 现象 | 先检查 |
| --- | --- |
| 类型或设备缺失 | 字典是否查询成功，再按上表分别检查各层过滤；空集合可能来自 DAO 失败回退 |
| 资源有记录但实例没生成 | 工厂注册、实际 Type、构造异常；保留加载阶段日志 |
| 列表存在但主区域没有页 | 信息页与显示页区别、`IDisPlayControl`、生成显示区的调用点 |
| 显示在线但动作失败 | 通信身份、具体命令/返回、真实设备状态；在线不是动作成功 |
| 手动成功但 Flow 失败 | 节点引用的设备 Code、模板版本和输入，再查共享会话完成条件 |
| 保存后异常或重启未生效 | 配置持久化和 RC 重启是不同阶段，进入[配置契约](../../01-user-guide/devices/configuration.md) |

另检查相机、算法、校准未初始化详情的释放，以及相机和校准内存结果模型的首次显示。它不构造真实设备连接、不执行 DAO 结果回查，也不覆盖 Spectrum 完整释放。测试引用不表示已执行或通过。

本页未声明资源树、工厂与真实 MySQL 的自动化集成覆盖。`ServiceConfigTests` 只验证注册中心服务信息属性通知，不证明此装配链；具体设备测试从对应主题进入。验证应记录同一设备的资源 ID/Code、版本、父终端、配置来源和实际失败阶段，敏感配置须脱敏。

获得授权后的隔离验证还需覆盖：查询失败发生在 Dispose/集合清理之后、`LastGenControl` 与调用者集合共享引用、构造中途失败、显示生成前后的对象身份，以及 **进入采集窗口** 与标题栏关闭。字段、路径和检索校验不覆盖这些运行时行为。

真机验收另按[现场证据规范](../../01-user-guide/field-operation-acceptance.md)授权执行。电机运动、SMU 输出、相机触发、远端重启和文件写入都不是文档校验或设备列表查看的附带动作。
