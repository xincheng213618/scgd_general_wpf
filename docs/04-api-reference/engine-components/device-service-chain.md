---
knowledge_id: "engine.devices"
knowledge_type: "topic"
status: "current"
summary: "设备工厂、资源重载与显示装配；旧对象释放、集合重建和显示替换并非一个事务，记录存在、默认可见、服务在线和动作完成分别判断。"
aliases: ["设备打不开","设备服务","设备连接","设备资源有记录却不出现","如何新增设备服务","设备资源重载","设备资源过滤","设备类型字典","组关联资源","空设备树","管理员服务配置","进入采集窗口","切换列表","LastSelectIndex","运行对象生命周期","显示集合","ServiceManager","t_scgd_sys_resource","t_scgd_sys_resource_group","DeviceServiceFactoryRegistry","ServiceTypes","LoadServices","LastGenControl"]
code_paths: ["Engine/ColorVision.Engine/Dao/SysResourceModel.cs","Engine/ColorVision.Engine/Dao/SysDictionaryModel.cs","Engine/ColorVision.Engine/Dao/SysResourceGoupModel.cs","Engine/ColorVision.Engine/Dao/VSysResourceDao.cs","UI/ColorVision.Database/BaseTableDao.cs","Engine/ColorVision.Engine/Services/ServiceManager.cs","Engine/ColorVision.Engine/Services/ServiceInitializer.cs","Engine/ColorVision.Engine/Services/WindowService.xaml.cs","Engine/ColorVision.Engine/Services/WindowService.xaml","Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs","Engine/ColorVision.Engine/Services/Type/TypeService.cs","Engine/ColorVision.Engine/Services/DeviceService.cs","UI/ColorVision.UI/DisPlayManager.cs"]
test_paths: []
related: ["engine.index","operations.device-configuration","engine.mqtt","engine.rc-registration","operations.camera","operations.motor","operations.smu","operations.calibration","operations.file-server","operations.flow-device","flow.session","ui.property-grid","ui.database"]
---

# Engine 设备资源与运行装配

`Services/` 中的设备服务不是通用 DI 服务。数据库资源经类型字典和工厂生成 `DeviceService`，设备列表、显示页、MQTT 通信和 Flow 再使用这些对象。**资源存在、设备被装配、显示页可见、服务在线、实际动作成功是不同状态。**

本页覆盖设备资源筛选、运行对象重建、显示装配与类型扩展。配置创建、导入、保存和删除见[设备资源配置与持久化](../../01-user-guide/devices/configuration.md)。

## 初始化与资源树

`ServiceInitializer.Order=5`；MySQL 已连接时，初始化物理相机管理器、服务集合、待应用 RC 更新和设备显示控件，再调用相机资源初始化。未连接时跳过服务配置。`ServiceManager` 构造器在 MySQL 已连接时通过 UI Dispatcher 调用 `LoadServices()`，并订阅后续连接变化；创建单例不是无副作用的只读查询。

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

`LoadServiceResourceSnapshot` 则直接执行数据库查询，没有本地捕获；此处抛出时，旧对象可能已被 Dispose，设备集合可能因共享引用已被清空，但旧类型树或显示项仍在；更晚失败也可能留下部分新集合。不能把“加载失败”解释为旧运行状态完整保留。具体设备是否释放全部句柄和事件取决于其 Dispose 实现；删除时的清理限制集中在[设备配置契约](../../01-user-guide/devices/configuration.md#导入、导出、重置与删除)。

构造器只在 MySQL 已连接时首次重载，但其 `MySqlConnectChanged` 订阅没有按新的连接值过滤就调用 `LoadServices()`。不能认为断开通知天然是无操作，也不要在只读诊断中用切换数据库连接或重载来试探；这些动作可能影响运行设备和旧窗口。

## 工厂存在不等于默认可见

`DeviceServiceFactoryRegistry.RegisterDefaults()` 是内置注册的来源，不是扫描所有 `Device*` 类自动发现。它注册 Camera、PG、Spectrum、SMU、Sensor、FileServer、Algorithm、FilterWheel、Calibration、Motor、ThirdPartyAlgorithms、Flow 和 LightingControl。

默认类型树明确过滤 **FileServer、FocusRing、Flow、ThirdPartyAlgorithms、ThirdPartyAlgorithms32、PowerControl**。因此 FileServer/Flow 有工厂仍不会生成它们自己的默认类型分支；LightingControl（值 16）没有被该过滤排除。过滤发生在类型节点层，装配可见终端的子资源时仍按子资源自己的 Type 查工厂，并未再次排除这些类型。因此遗留或错误层级数据仍可能经其它类型终端构造它们，不能将类型过滤说成全局禁止实例化。不要仅凭有实现类或菜单文字承诺可创建、可见或可运行。

`CreateService(resource)` 在工厂未注册时返回 `null`，该资源不会进入运行集合；工厂构造抛异常则会向外传播，不是同一种“跳过”行为。重复注册默认抛错，明确 `replace=true` 才替换既有工厂，不能无意覆盖其它模块的类型所有者。

## 设备树与显示区

1. 打开 **工具 → 管理员服务配置**（`WindowService`）。列表模式由 `ShowType2` 配置决定，初始值为设备；**切换列表** 按设备 → 类型 → 终端循环。
2. 选择设备查看 `GetDeviceInfo()`；选择终端或类型节点查看 `GenDeviceControl()`。这里是信息/配置页，不是主界面的设备控制页。
3. 点击底部 **进入采集窗口**，为当前资源树中的设备生成主显示区并关闭配置窗口，并非只打开刚才选中的设备。标题栏关闭只关闭窗口，不执行这次显示生成。

`GenDeviceDisplayControl()` 沿当前类型树生成主显示区；`GenControl(collection)` 使用指定设备集合。两者都先加入共享 `DisplayFlow`，只有设备 `GetDisplayControl()` 返回 `IDisPlayControl` 才追加该页，最后通过 `DisPlayManager.ReplaceControls` 替换显示集合。

`LoadServices()` 最后发布 `ServiceChanged`，但本身不调用 `GenDeviceDisplayControl()` 或 `ReplaceControls()`；释放设备、清空 `LastGenControl` 也不等于替换主显示集合。初始化器和 **进入采集窗口** 的 `Button_Click` 会另行生成显示区；`OnClosed()` 只处理上下文和窗口关闭。因此资源集合、主显示项和旧窗口引用可能处于不同轮次；新增资源后列表出现、主区域出现、Flow 能按正确 Code 绑定，应分别核对，不能只检查一个窗口。

`ReplaceControls` 清空并重新填充显示集合，恢复分组和排序后按 `LastSelectIndex` 选择控件；索引越界时选第 0 项。它不按设备 Code 恢复原来的选中设备，也不调用旧控件的 Dispose。设备增减或排序变化后，应核对实际选中对象，不能仅凭界面仍有选中项推断身份未变。

## 各模块的独立契约

| 问题 | 所属实现 | 主题 |
| --- | --- | --- |
| 相机服务、取图与运行参数 | `Services/Devices/Camera/` | [相机服务](../../01-user-guide/devices/camera.md) |
| 物理相机、许可、校准配置 | `Services/PhyCameras/` | [物理相机](../../01-user-guide/devices/camera-management.md)、[相机配置](../../01-user-guide/devices/camera-configuration.md) |
| 运动及位置状态 | `Services/Devices/Motor/` | [电机](../../01-user-guide/devices/motor.md) |
| 电压/电流与扫描输出 | `Services/Devices/SMU/` | [SMU](../../01-user-guide/devices/smu.md) |
| 本地校正与服务校准 | `Services/Devices/Calibration/` | [校准](../../01-user-guide/devices/calibration.md) |
| 文件服务资源与实际文件输出 | `Services/Devices/FileServer/` | [文件服务](../../01-user-guide/devices/file-server.md) |
| 远端 Flow 服务与本地图的区别 | `Services/Devices/FlowDevice/` | [流程设备](../../01-user-guide/devices/flow-device.md) |

PG、Spectrum、Sensor 等设备从 `RegisterDefaults` 定位具体配置、命令和显示实现；插件同名不等于同一个设备对象。MQTT 关联、返回与超时由[消息契约](../../02-developer-guide/engine-development/mqtt.md)维护，Flow 业务完成由[执行会话](../../01-user-guide/workflow/execution.md)维护。

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

本页未声明资源树、工厂与真实 MySQL 的自动化集成覆盖。`ServiceConfigTests` 只验证注册中心服务信息属性通知，不证明此装配链；具体设备测试从对应主题进入。验证应记录同一设备的资源 ID/Code、版本、父终端、配置来源和实际失败阶段，敏感配置须脱敏。

获得授权后的隔离验证还需覆盖：查询失败发生在 Dispose/集合清理之后、`LastGenControl` 与调用者集合共享引用、构造中途失败、显示生成前后的对象身份，以及 **进入采集窗口** 与标题栏关闭。字段、路径和检索校验不覆盖这些运行时行为。

真机验收另按[现场证据规范](../../01-user-guide/field-operation-acceptance.md)授权执行。电机运动、SMU 输出、相机触发、远端重启和文件写入都不是文档校验或设备列表查看的附带动作。
