---
knowledge_id: "operations.data"
knowledge_type: "index"
status: "current"
summary: "按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。"
aliases: ["数据管理", "数据在哪里", "数据库文件位置", "备份包含哪些数据", "ConfigFilePath", "SqliteDbPath", "MsgRecords.db", "FlowNodeRecords.db", "SocketMessages.db", "Spectrum.db", "ProjectARVRPro.db", "数据没保存"]
code_paths: ["UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.Database/MySqlControl.cs", "UI/ColorVision.Database/DatabaseBrowserProviderRegistry.cs", "Engine/ColorVision.Engine/Dao/SysResourceModel.cs", "Engine/ColorVision.Engine/Dao/AlgResultMasterDao.cs", "Engine/ColorVision.Engine/Messages/MessagesListManager.cs", "Engine/ColorVision.Engine/Messages/MsgRecordManagerConfig.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeRecordDataBaseHelper.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeRecordConfig.cs", "UI/ColorVision.SocketProtocol/SocketMessageManager.cs", "Plugins/Spectrum/Data/ViewResultManager.cs", "Projects/ProjectARVRPro/ViewResultManager.cs"]
test_paths: []
related: ["ui.database", "ui.database-query", "engine.database-maintenance", "engine.mysql-maintenance", "ui.sqlite-storage", "ui.configuration", "operations.exports", "operations.device-configuration", "engine.results", "flow.templates", "ui.socket-protocol", "plugins.spectrum", "projects.arvr-pro"]
---

# 数据所有者与存储定位

设置中的[存储与维护](../../04-api-reference/ui-components/storage-maintenance.md)集中提供历史日志、明确临时产物和可重建缓存的手动清理，以及独立的数据维护、备份与选择性设置重置入口。它不是“总数据库”或全盘垃圾扫描器，以下数据所有者边界仍然适用。

ColorVision 没有一个覆盖软件设置、设备资源、流程模板、消息记录、业务结果和图片的“总数据库”。先确认对象由谁写入、谁读取，再选择查询、导出或备份入口。`UI/ColorVision.Database` 提供通用基础和浏览器，不拥有所有模块的数据。

## 从对象找到实现

| 对象 | 所有者与实际存储入口 | 继续核对 |
| --- | --- | --- |
| 软件设置与界面配置 | `ConfigHandler.ConfigFilePath` 指向配置 JSON；`ConfigService` 提供配置对象 | [配置持久化与重载](../../04-api-reference/ui-components/configuration.md)、[设置导入导出](./export-import.md) |
| Engine 设备资源与配置 | MySQL `t_scgd_sys_resource`；`SysResourceModel.Value` 对应 `txt_value` | [资源配置、保存与重启](../devices/configuration.md) |
| 流程与关联模板 | Engine 的 `TemplateFlow` 及模板 DAO；运行图、保存模板和导出包不是一份对象 | [Flow 模板与持久化](../../04-api-reference/engine-components/template-flow-chain.md) |
| Engine 算法历史结果 | MySQL `t_scgd_algorithm_result_master` 与所属明细 DAO；`ImgFile` / `ResultImagFile` 是路径字段 | [结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) |
| Engine MQTT 消息记录 | `MessagesListManager` / `MsgRecordDataBaseHelper`，路径取 `MsgRecordManagerConfig.SqliteDbPath` | [MQTT 消息契约](../../02-developer-guide/engine-development/mqtt.md) |
| Flow 节点诊断记录 | `FlowNodeRecordDataBaseHelper`，路径取 `FlowNodeRecordConfig.SqliteDbPath`，有独立写队列 | [Flow 执行与最终化](../workflow/execution.md) |
| Socket 收发记录 | `SocketMessageManager.SqliteDbPath`，与 Engine MQTT 消息库分开 | [Socket 协议模块](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) |
| Spectrum 测量结果 | `Plugins/Spectrum/Data/ViewResultManager.cs` 管理自身 SQLite | [Spectrum](../../04-api-reference/plugins/standard-plugins/spectrum.md) |
| ARVR 项目结果 | `Projects/ProjectARVRPro/ViewResultManager.cs` 管理自身 SQLite 与历史结果模型 | [ProjectARVRPro](../../04-api-reference/projects/project-arvr-pro.md) |
| 原图、标注图、CSV/MES 等交付物 | 所属设备、ImageEditor 或项目 exporter，不能从一条数据库记录推出文件完整性 | [导入导出边界](./export-import.md) |

这是已定位的主要责任，不是所有插件和客户包的完整存储清单。添加或修改数据路径时，检查该模块的实际配置和写入入口，不把相同类名 `ViewResultManager` 当成共享数据库。

## 路径与数据源发现

软件设置的具体路径选择与文件名规则统一见[配置路径契约](../../04-api-reference/ui-components/configuration.md)，以实际 `ConfigFilePath` 为准，不能固定猜成仓库或可执行文件旁的 `Config`。设置 JSON 的路径与模块自己的 SQLite 路径不是同一契约，迁移配置文件不等于一起迁移数据库。

模块的当前默认 SQLite 位置如下，实际查询仍以当次配置或静态属性值为准：

| 模块 | 默认路径，`ApplicationData` 指 Windows 当前用户的漫游应用数据目录 |
| --- | --- |
| Engine MQTT 消息 | `ApplicationData/ColorVision/Config/MsgRecords.db` |
| Flow 节点诊断 | `ApplicationData/ColorVision/Config/FlowNodeRecords.db` |
| Socket 消息 | `ApplicationData/ColorVision/Config/SocketMessages.db` |
| ARVR 结果 | `ApplicationData/ColorVision/Config/ProjectARVRPro.db` |
| Spectrum 结果 | `ApplicationData/Spectromer/Config/Spectrum.db`；`Spectromer` 是代码中的实际拼写 |

MQTT/Flow 的 `SqliteDbPath` 是 `DirectoryPath` 加固定文件名的只读属性；Socket、Spectrum、ARVR 的同名静态属性则可被替换。修改已初始化的对象路径还要核对其持有连接和初始化状态，不能当成在线切库协议。MySQL 则由 `MySqlControl` 使用当前 `MySqlConfig` 的服务器、端口和数据库名建立连接，不是本地 `.db` 文件。

数据库浏览器默认注册 MySQL，其它 SQLite 来源需要所属模块注册 Provider。例如消息记录、Flow 诊断、Spectrum 和 ARVR 的注册都在各自初始化路径中。因此“左侧树里没有”不证明磁盘文件或历史记录不存在。表结构查询、主键与写入规则统一见[数据库浏览与行级维护](../../04-api-reference/ui-components/ColorVision.Database.md)。

## 查数据前的副作用边界

查源码里的路径是只读动作，调用运行时单例不一定是：`ConfigHandler` 首次单例加载会初始化配置/Backup 目录，并在 `IsAutoSave` 开启时安排备份（空构造函数本身不执行这些动作）；`SocketMessageManager` 会创建目录、建表及补充 schema；Spectrum/ARVR 的结果管理器初始化各自表结构；Flow 诊断初始化还会迁移 schema 并启动写线程。不要为了回答“数据库在哪”就调用这些初始化路径或启动整套应用。

同样，业务对象的内存集合、数据库行、图片文件和协议输出分别有自己的完成条件：

- `MessagesListManager.MsgRecordsClearCommand` 与 `SocketMessageManager.MessagesClearCommand` 只清当前内存列表，不等于删除数据库记录。
- Engine 主结果中的图片路径不包含原始像素；有主记录不证明图片仍存在，有预览图也不证明全部明细成功落库。
- `.cvsettings` 只覆盖配置服务管理的设置；`.cvflow`、项目数据库、原图及项目报告各自核对。导出一种对象不构成全项目备份。
- 日志、MQTT/Socket 消息和 Flow 诊断帮助定位执行阶段，不能单独替代业务结果查询或外部系统的最终回执。

## 最小核验顺序

1. 固定对象类型、版本、设备 Code / SN / 批次或主结果 ID，确定写入代码与读取代码是否指向同一库、同一路径。
2. 在已有授权范围内只读检查来源、过滤条件及页码；不为排障触发采集、重载设备、创建数据库或重跑流程。
3. 将证据拆开：写入返回与异常、重新查询的记录、实际文件及其可读取性、业务最终状态。需要导出时进入该对象的规范主题。
4. 只有明确要求数据维护后才讨论备份、迁移和清理。使用模块已有的维护入口并核对它的停写、锁与恢复条件；“撤销界面编辑”或“重置数据库”都不是通用备份恢复策略。Socket/Flow 的压缩正文与旧 TEXT 迁移、强制备份入口和部分提交边界见 [SQLite 存储与维护](../../04-api-reference/ui-components/sqlite-storage.md)。

业务页面的[通用查询窗口](../../04-api-reference/ui-components/database-query.md)按实体构造条件并替换调用方结果集合；它不是数据库浏览器，SQL 预览、会话保存和整表操作须分别核对，不能把“当前只显示这些行”当成删除范围。

另一个[数据库维护窗口](../../04-api-reference/engine-components/database-maintenance.md)以 provider 组织表统计、按月清理、备份和迁移；统计不是删除预览，关闭不取消后台维护。MySQL 的结果表白名单、历史删除与整表截断、SQL 备份及部分失败边界见 [MySQL 结果维护](../../04-api-reference/engine-components/mysql-maintenance.md)。

本页是跨模块定位入口，未声明覆盖上述所有存储、迁移和断电恢复的统一测试。对应主题引用的局部测试也不证明跨 MySQL、SQLite、文件和外部协议存在一个共同事务。
