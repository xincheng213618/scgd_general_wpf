---
knowledge_id: "ui.database"
knowledge_type: "topic"
status: "current"
summary: "MySQL 连接配置与业务 DAO、POI 模板的 SQLite 自动回退，以及旧插件注册的二进制兼容。"
aliases: ["数据库操作", "数据库浏览器", "连接数据库", "SQL", "MySQL", "SQLite", "ColorVision.Database", "BaseTableDao", "MySqlControl", "BatchExecuteNonQuery", "数据库事务回滚", "LegacyDatabaseBrowserRegistration", "ColorVision.Local.db", "POI 本地模板", "PoiTemplateStorage"]
code_paths: ["UI/ColorVision.Database", "UI/ColorVision.Database/Compatibility/LegacyDatabaseBrowserRegistration.cs", "Engine/ColorVision.Engine/Templates/POI/PoiTemplateStorage.cs", "Engine/ColorVision.Engine/Media/PoiImageViewComponent.cs"]
test_paths: ["Test/ColorVision.UI.Tests/BatchExecuteNonQueryTests.cs", "Test/ColorVision.UI.Tests/LegacyDatabaseBrowserRegistrationTests.cs", "Test/ColorVision.UI.Tests/LocalPoiTemplateStorageTests.cs"]
related: ["ui.database-query", "ui.sqlite-storage", "engine.database-maintenance", "operations.data", "ui.configuration", "ui.desktop", "flow.session"]
---

# 数据库连接、DAO 与旧插件兼容

`UI/ColorVision.Database/` 提供 MySQL 连接配置、业务 DAO、实体驱动的[通用查询](./database-query.md)，以及 [SQLite 正文存储、迁移与维护](./sqlite-storage.md)。

`LocalTemplateStore` 管理当前用户 `ApplicationData/ColorVision/Config/ColorVision.Local.db`，SQLite 引擎随应用提供，无需安装数据库服务。首次使用自动建库；`local_templates` 按 `kind` 区分模板类型，保存名称、业务 schema 版本、完整 JSON 和顺序。当前接入 POI 与流程配置，后续类型由各 owner 定义 payload；该库只保存配置，不保存图片、检测结果、运行日志或执行记录。POI 及其全部点位作为一个文档原子保存，点位保留小数坐标；流程保存完整 STN 与稳定身份，并通过条件更新防止并发覆盖。读取失败和不支持的业务版本必须报错，不能当成空模板。

POI 的 `PoiTemplateStorage` 在 MySQL 未连接或列表查询失败时自动使用本地库，不主动调用 `MySqlControl.Connect()`。ImageView 底部模板框旁的管理图标打开现有模板管理器，并选中当前模板；保存图标将当前图上的 POI 区域保存到所选模板，未选模板时输入名称新建。保存保留当前画布，支持矩形、圆形和已有点位；旋转矩形、椭圆及不能完整还原的形状会提示调整，避免改变保存后的几何。模板下拉箭头保留，工具栏无溢出项目时隐藏“更多”箭头。下拉框右键可编辑当前模板或刷新，管理器标题和下拉框提示标识当前存储。新建、编辑、重命名、复制、删除、排序和 `.cfg` 导入导出均走该存储入口。文件导入及导出不要求宿主 Solution 初始化。

POI 本地 ID 使用小于 -1 的值，-1 保留为空模板；MySQL ID 保持正数，两库同名模板独立，不自动同步。已经打开的本地模板即使 MySQL 恢复也继续读写本地；重新打开/刷新模板列表重新判定可用后端。POI 服务器保存失败时，只有已完整加载明细的模板才能保留为新的本地副本，并显示本地保存提示，不能拿服务器 ID 覆盖本地同号记录。未加载明细的服务器模板断线后禁止保存不完整副本，重新打开列表即可使用本地模板。流程按其所属存储保存，服务器流程断线时保留失败，不自动生成本地副本；其本地管理、STN 导入与运行限制见流程模板主题。其他 DAO、服务算法和 MySQL 表结构保持原有契约。

`DatabaseCleanupWindow` 虽使用同一命名空间，源码实际属于 Engine，是独立的[多数据源维护宿主](../engine-components/database-maintenance.md)。

## 已发布插件的注册兼容

旧版 ARVR、LUX、Spectrum 等插件会在结果管理器初始化时调用 `new SqliteDatabaseBrowserProvider(...)` 和 `DatabaseBrowserProviderRegistry.Register(IDatabaseBrowserProvider)`。直接删除这些公开类型，会让仍在运行的旧插件在启动时出现类型或方法缺失。

`Compatibility/LegacyDatabaseBrowserRegistration.cs` 仅保留这组已发布签名：空的 `IDatabaseBrowserProvider` 接口、四参数 SQLite Provider 构造函数和静态 `Register`。它们均标记为过时；构造和注册不读取路径、不调用或持有 client factory、不保存 Provider、不连接数据库，也不贡献工具入口。这个兼容文件不提供原浏览器的查询、CRUD 或扩展能力，不能作为新增数据源接口使用。

打开连接会访问数据库，业务写入、批 SQL、迁移和清理仍会改变真实数据。只读排障不授权执行这些操作；数据归属和备份范围见[数据责任地图](../../01-user-guide/data-management/README.md)。

## MySQL 连接配置与副作用

连接来源是 `MySqlSetting.Instance.MySqlConfig`（通过 `ConfigService` 管理），并保留配置集合。软件配置的保存与重载见[配置契约](./configuration.md)。修改配置不会自动重建所有业务模块已持有的连接，需核对各自的连接生命周期。

`MySqlControl.GetConnectionString` 使用配置中的 Host/Port/UserName/UserPwd/Database，统一 `utf8mb4`，默认连接超时 1 秒，测试连接使用 2 秒；这不是查询执行超时。当前字符串明确 `SSL Mode=None`、开启连接池及本地 infile 选项，不能宣传成默认加密连接。配置保存使用 `IConfigSecure` 的密码加解密钩子，不是外部凭据保险库，连接字符串和配置导出仍应视为敏感数据。

默认连接窗口和状态栏入口由 Engine 宿主，源码位于 `Engine/ColorVision.Engine/Mysql/MySqlConnect.xaml` 与 `MySqlSettingProvider.cs`；Database 只保留共享配置、连接状态和 DAO 能力。窗口暂时保留 `ColorVision.Database.MySqlConnect` 类型名，供现有 Engine 及插件源码继续使用，不代表窗口仍由 Database 程序集实现。

几个容易混淆的入口要分开：

- `TestConnect(config)` 真实联网打开连接，再用参数化查询检查库是否存在；成功不验证全部表权限或业务读写，也不会调用全局 local_infile 修改。
- `Connect()` 不只是探活：打开连接后查询 `@@global.local_infile`，为 0 时尝试 `SET GLOBAL local_infile = 1`；随后才设 IsConnect 并通知订阅者。它需要额外数据库权限，可能改变服务器全局设置并触发宿主后续装配，不应为文档或只读诊断随意调用。
- `MySqlConnect` 窗口直接绑定共享配置；选择配置项会立即替换当前对象，确认会启动后台 Connect 然后关闭，并不等待连接完成，也没有直接调用配置服务落盘。“取消”仅把打开窗口时的备份复制进当前选择对象，普通关闭只执行清理；不要把这个多配置窗口当成完整事务式编辑会话。

`IsConnect` 是一次 Connect 流程维护的状态，不是每条 SQL 的健康证明。业务 DAO 和直接创建的 client 仍须分别核对连接门禁。

## 业务 DAO 与批 SQL

`BaseTableDao<T>` 的业务方法主要由扩展类提供，依赖 `IEntity`、业务实体及 SqlSugar 映射，与实体驱动的通用查询窗口分别维护调用契约。这些扩展先检查 `MySqlControl.IsConnect`，不少读取失败返回空集合、null、false 或 0，写入失败返回 -1 并记录日志；因此 DAO 的“空结果”可能是连接未就绪或被吞掉的异常。具体调用者仍须核对方法返回语义，不把基类封装当成事务、重试或业务成功保证。

`MySqlControl.BatchExecuteNonQuery` 是直接执行批 SQL 的通道。它在一个 executor 上 BeginTransaction，逐项执行并 Commit，提交成功才返回累加影响行数；失败尝试 Rollback，并抛出含阶段、非空语句序号和错误类型/代码的 `BatchExecuteNonQueryException`。回滚失败保留原故障，提交后 Dispose 失败只记清理警告，不改成“未提交”。

该批入口仅按分号切分，不是理解引号、注释或存储过程的 SQL 解析器；传入的是 SQL 文本，不是通用参数化接口。MySQL DDL/隐式提交语句不能因包装了事务就声称可回滚。执行、迁移、恢复和建表必须走各自授权与恢复契约，不把这个函数作为任意 SQL 都可回滚的保证。

## 验证入口与缺口

- `LocalPoiTemplateStorageTests.cs` 在隔离临时 SQLite 文件验证离线读写、重启恢复、精度、导入导出、排序、存储隔离及 ImageView 编辑保存链；MySQL 故障通过禁止访问/抛错的 factory 模拟，不验证真实 MySQL 权限或硬件。
- `BatchExecuteNonQueryTests.cs` 用 fake executor 核对事务阶段、失败诊断和清理语义，不连接真实 MySQL，也不证明 DDL 可以回滚。
- `LegacyDatabaseBrowserRegistrationTests.cs` 按旧插件的精确类型、构造函数和方法签名反射绑定，再用禁止调用的路径/client factory 验证注册无数据库副作用；它不承诺旧浏览器的全部公开 API 继续可用。
- 数据库清理窗口的独立接口和窗口测试见[维护窗口契约](../engine-components/database-maintenance.md)。真实账号权限、连接生命周期、恢复和数据完整性仍由各业务 owner 验证。

目标框架和依赖以 `ColorVision.Database.csproj` 为准：当前 WPF 多目标 `net8.0-windows7.0;net10.0-windows7.0`，依赖 SqlSugar、SQLite runtime、Newtonsoft.Json、log4net 和 ColorVision.UI。缺 DLL、连接失败、真实库结构不符以及业务结果未完成应分别定位；该模块本身不安装数据库服务。
