---
knowledge_id: "ui.database"
knowledge_type: "topic"
status: "current"
summary: "数据库 Provider、表浏览和 MySQL/DAO 契约；区分读取、行级写入、内存撤销与事务，保存可能部分成功。"
aliases: ["数据库操作", "数据库浏览器", "连接数据库", "SQL", "MySQL", "SQLite", "ColorVision.Database", "IDatabaseBrowserProvider", "DatabaseBrowserProviderRegistry", "CanWriteCurrentTable", "BatchExecuteNonQuery", "数据库修改撤销", "数据库逐行保存失败", "数据库事务回滚"]
code_paths: ["UI/ColorVision.Database", "UI/ColorVision.Common/ThirdPartyApps/ThirdPartyAppInfo.cs"]
test_paths: ["Test/ColorVision.UI.Tests/BatchExecuteNonQueryTests.cs", "Test/ColorVision.UI.Tests/ThirdPartyAppInfoTests.cs"]
related: ["ui.database-query", "ui.sqlite-storage", "engine.database-maintenance", "operations.data", "ui.configuration", "ui.desktop", "flow.session"]
---

# 数据库 Provider、表浏览与写入契约

`UI/ColorVision.Database/` 提供真实表结构驱动的数据库浏览器、MySQL 连接配置和业务 DAO 基础。浏览器不是任意 SQL 控制台，也不是业务结果完成状态的权威判断器；表中有记录不等于 Flow 最终化、结果导出或设备操作已经完成。

同模块的另外两条独立链路分别见[实体驱动的通用查询](./database-query.md)和 [SQLite 正文存储、迁移与维护](./sqlite-storage.md)。通用查询没有浏览器的同一套分页、后台取消和写入门禁；底层存储工具也不自动接管业务停写、备份与恢复。

`DatabaseCleanupWindow` 虽使用同一命名空间，源码实际属于 Engine，是[多数据源维护宿主](../engine-components/database-maintenance.md)，其统计、确认和备份接口与本页行级 CRUD 不同。

打开数据源会访问数据库，新增、保存和删除会直接写真实表。解释或排障任务不授权连接未知数据源、读取敏感记录、修改连接、执行 SQL 或写入数据。真实验证前明确目标库、账号权限、影响范围和恢复方式；不要对生产库做写入烟测。各数据库、结果文件和清理工具的责任归属见[数据责任地图](../../01-user-guide/data-management/README.md)。

## 入口、注册与数据源发现

当前内部工具项由 `DatabaseBrowserAppProvider`（`MenuEntityBrowser.cs`）提供，声明 `RequiredPermission = Administrator`；`ThirdPartyAppInfo` 的命令可执行条件和实际启动处理都会检查权限。这个门禁属于工具启动入口：`DatabaseBrowserWindow` 构造和 Provider 行接口没有同等的统一权限检查，不能把直接构造窗口或调用 Provider 当成经过了授权。

`DatabaseBrowserProviderRegistry` 的行为是：

- 首次 `GetProviders()` / 有效 ID 的 `GetProvider()` 懒注册默认 `mysql.default`，配置由 `MySqlControl.CreateBrowserProvider()` 提供。不扫描实体、程序集或磁盘自动发现其它数据源。
- SQLite Provider 必须由调用方显式注册；日志、流程诊断、插件或项目库能否出现取决于其所属模块是否已经初始化并执行注册。存在 `CreateBrowserProvider` 方法不等于默认可见。
- `Register` 按不区分大小写的 ProviderId 替换同名项，不是拒绝重复；默认注册也会替换之前已放入的同 ID 项。
- 返回的是 Provider 列表副本。浏览器 `LoadProviders()` 建立树，后续注册不会自动更新已打开窗口；刷新数据源会重新取列表，并清空当前表视图。

树逐层调用 `GetDatabases → GetTables → GetColumns/QueryPage`，这些不是纯内存查询。MySQL Provider 从 `INFORMATION_SCHEMA` 列出账号可访问的非系统库，再列 `BASE TABLE`，不是仅浏览配置中的一个数据库，也不列出视图。SQLite Provider 使用调用方提供的路径和 client factory，展示 `main` 下非 `sqlite_%` 表；路径会展开环境变量，连接是否创建文件或附带初始化动作必须检查实际 factory。

## 浏览、搜索、分页与排序

浏览器按列元数据生成 `DataTable` 和网格，不要求业务 C# 实体。默认第 1 页、每页 50 行，可选 10/20/50/100；默认按第一主键列降序，没有主键时退到第一列。点击列头切换排序并返回第 1 页，搜索和清空搜索也返回第 1 页。

两个内置 Provider 都用参数化 `LIMIT/OFFSET` 分页，但以下边界不可省略：

| 行为 | 实际范围与限制 |
| --- | --- |
| 关键字 | 仅在标为 `IsTextLike` 的列间做 OR-LIKE；不是所有数字、时间、二进制字段的全文检索 |
| 匹配值 | trim 后以 `%keyword%` 传参；没有转义 LIKE 的 `%` / `_` 通配符，不保证逐字匹配 |
| 无文本列 | 搜索条件为空，关键字不会进一步过滤 |
| 排序 | 列名需匹配元数据，找不到则退回第一主键/第一列；只按一个字段排序，没有追加唯一键消除同值顺序不确定性 |
| 记录数与页内容 | COUNT 与 SELECT 分别查询，没有共同事务快照；并发写入可能使数量和页内容不一致 |
| 删除后页码越界 | 先查原页，再在 `UpdatePagination()` 钳制显示页码；该轮不会自动重查钳制后的页，可能需再次刷新 |

查询由 `Task.Run` 承载同步 Provider 调用。切表或关闭时的取消令牌用于停止接受旧结果，接口不向数据库调用传入令牌，不能宣称正在执行的 SQL 已被取消。错误显示在节点/状态区并记录日志；读取异常不等于表确实为空。

## 行级写入门禁与主键

`CanWriteCurrentTable` 只检查 `provider.CanWrite && table.CanWrite`，不检查 `DatabaseCatalogInfo.CanWrite`、数据库实际 GRANT、业务权限或当前登录身份。内置基类默认 `CanWrite = true`；这表示界面能力声明，不代表账号一定能写或用户已授权 AI 操作。

| 操作 | 当前门禁和行为 |
| --- | --- |
| 新增 | Provider 和表声明可写即可，**不要求已有主键**；独立 `DatabaseRowEditWindow` 确认后立即调用 `InsertRow`，不是待保存的草稿 |
| 网格修改 | 还要求至少识别出一个主键；主键、自增列、只读列不可编辑，网格也禁用直接新增/删除行 |
| 保存修改 | 提交当前网格编辑，只处理 Added/Modified 行；有 Modified 行且无主键时拒绝 |
| 删除 | 可写且有主键，选中单行并二次确认后立即调用 `DeleteRow`；不是先标记删除再等“保存” |
| 撤销 | 有表即可，调用当前 `DataTable.RejectChanges()`；不会向数据库发送逆操作 |

更新使用 `DataRowVersion.Original` 的主键值；删除已修改行也取原主键，否则取当前值。`BuildKeys` 收集元数据中的主键列、拒绝取到的 null 主键和空键集合，但会跳过结果集缺失的主键列：自定义 Provider 必须保证页数据带齐主键，不能仅凭这个方法宣称验证了完整复合主键。

更新 WHERE 只有主键，没有版本号或原字段值的乐观并发检查；`BuildValues` 会提交行中所有可写非主键列，不只是编辑过的字段。刷新前别的客户端更新过同一行时，保存可能覆盖对方改动。Provider 返回影响行数，不提供“恰好一行”和业务约束已满足的额外证明。

新增窗口排除自增/只读列，其余字段逐个转换；空白输入成为 null，而不是省略字段以使用数据库默认值。数字按不变区域格式解析，日期/时间按当前区域格式解析；这不是完整数据库类型编辑器。NOT NULL、外键、长度、生成列等最终约束仍可能由数据库拒绝。SQLite 的自增识别采用“主键且声明类型含 INT”启发式，不能推导它正确覆盖所有复合键或特殊表定义。

## 参数化的保证与未覆盖范围

`DatabaseBrowserProviderBase` 为值、关键字和行键构造 `SugarParameter`；MySQL 标识符用反引号转义，SQLite 用双引号转义。写入列从元数据筛选，更新排除主键和自增列。不能把这些措施泛化为“所有数据库操作自动安全”：

- 内置 Provider 的 `InsertRow/UpdateRow/DeleteRow` 不自行检查 CanWrite、管理员权限或操作授权；绕过 UI 的调用者必须守住这些条件。
- SQL 构造器更新/删除只要求 keys 非空，不验证调用者传入的键一定是完整主键。标识符转义不是表/字段授权。
- 基类插入/更新筛选没有独立排除 `IsReadOnly`；当前 UI 会过滤只读列，自定义调用者不能省略这层检查。
- 参数化不能防止合法 SQL 修改了错误的库、表或行，也不替代备份、业务约束、事务和并发保护。

扩展时实现 `IDatabaseBrowserProvider` 的元数据、分页和三个行接口，在明确初始化位置注册唯一 ProviderId；只读来源应在 Provider/表能力和实际写入口共同限制。不要为浏览新数据源复制实体类或直接拼接外部输入为 SQL。

## 脏数据、部分成功与“撤销”

加载一页会用查询结果替换当前 DataTable 并 `AcceptChanges()`，成为新的撤销基线。当前窗口没有统一的脏数据确认：切表、刷新数据源、翻页、排序、搜索、刷新表和关闭均可能丢弃未保存修改。成功新增/删除/保存后的重新查询也会替换页数据；不能指导用户把“刷新”当成保留草稿的检查方式。

`SaveChangesAsync()` 逐行调用 Provider；两个内置 Provider 每次行写入各自创建并释放 client，**浏览器没有包住整批修改的事务**。第 N 行失败会中止后续处理，但前 N−1 行可能已经提交。异常路径不统一重新查询或接受成功行的内存状态；直接重试可能重发已成功的操作。撤销只恢复内存，不会撤回此前成功的新增、删除或部分保存。

保存正常结束会尝试重新查询；查询失败由 `ReloadPageAsync()` 自己捕获，所以“保存调用结束”和“刷新看到了新数据”仍须分别确认。保存失败后先按原始主键核对实际持久化状态及失败阶段，不要假设全失败，也不要盲目重试。查询/界面状态不能代替[Flow 执行与最终化](../../01-user-guide/workflow/execution.md)的业务完成判据。

## MySQL 连接配置与副作用

连接来源是 `MySqlSetting.Instance.MySqlConfig`（通过 `ConfigService` 管理），并保留配置集合；不是浏览器自己保存的一份连接设置。软件配置的保存与重载见[配置契约](./configuration.md)。默认 Provider 持有取当前配置的委托，因此修改配置后后续调用会使用新值，旧树中的库名不会因此自动刷新。

`MySqlControl.GetConnectionString` 使用配置中的 Host/Port/UserName/UserPwd/Database，统一 `utf8mb4`，默认连接超时 1 秒，浏览器和测试连接使用 2 秒；这不是查询执行超时。当前字符串明确 `SSL Mode=None`、开启连接池及本地 infile 选项，不能宣传成默认加密连接。配置保存使用 `IConfigSecure` 的密码加解密钩子，不是外部凭据保险库，连接字符串和配置导出仍应视为敏感数据。

几个容易混淆的入口要分开：

- `TestConnect(config)` 真实联网打开连接，再用参数化查询检查库是否存在；成功不验证全部表权限或业务读写，也不会调用全局 local_infile 修改。
- `Connect()` 不只是探活：打开连接后查询 `@@global.local_infile`，为 0 时尝试 `SET GLOBAL local_infile = 1`；随后才设 IsConnect 并通知订阅者。它需要额外数据库权限，可能改变服务器全局设置并触发宿主后续装配，不应为文档或只读诊断随意调用。
- `MySqlConnect` 窗口直接绑定共享配置；选择配置项会立即替换当前对象，确认会启动后台 Connect 然后关闭，并不等待连接完成，也没有直接调用配置服务落盘。“取消”仅把打开窗口时的备份复制进当前选择对象，普通关闭只执行清理；不要把这个多配置窗口当成完整事务式编辑会话。

`IsConnect` 是一次 Connect 流程维护的状态，不是每条 SQL 的健康证明。浏览器的 MySQL Provider 自建 client，不以此状态为读取门禁；业务 DAO 则有不同规则。

## 业务 DAO 与批 SQL 不是浏览器保存

`BaseTableDao<T>` 的业务方法主要由扩展类提供，依赖 `IEntity`、业务实体及 SqlSugar 映射，不驱动浏览器动态表结构。这些扩展先检查 `MySqlControl.IsConnect`，不少读取失败返回空集合、null、false 或 0，写入失败返回 -1 并记录日志；因此 DAO 的“空结果”可能是连接未就绪或被吞掉的异常。具体调用者仍须核对方法返回语义，不把基类封装当成事务、重试或业务成功保证。

`MySqlControl.BatchExecuteNonQuery` 则是另一条直接执行 SQL 的通道，**浏览器多行保存没有调用它**。它在一个 executor 上 BeginTransaction，逐项执行并 Commit，提交成功才返回累加影响行数；失败尝试 Rollback，并抛出含阶段、非空语句序号和错误类型/代码的 `BatchExecuteNonQueryException`。回滚失败保留原故障，提交后 Dispose 失败只记清理警告，不改成“未提交”。

该批入口仅按分号切分，不是理解引号、注释或存储过程的 SQL 解析器；传入的是 SQL 文本，不是通用参数化接口。MySQL DDL/隐式提交语句不能因包装了事务就声称可回滚。执行、迁移、恢复和建表必须走各自授权与恢复契约，不把这个函数作为浏览器维护的“安全批量模式”。

## 验证入口与缺口

| 已有测试 | 实际证明范围 |
| --- | --- |
| `BatchExecuteNonQueryTests.cs` | 用 fake executor 核对创建/开始/语句/提交/回滚/释放失败、成功计数和诊断脱敏；不是连接真实 MySQL 或验证 DDL 回滚 |
| `ThirdPartyAppInfoTests.cs` | 通用内部工具权限层级、直接执行命令仍受权限门禁，以及数据库工具等 Provider 的元数据；不验证窗口内行写入授权 |

没有登记浏览器 CRUD、元数据差异、真实数据库权限、分页并发、脏数据丢弃或部分提交的专用自动化覆盖。隔离验证应包含只读源、无主键新增、复合主键缺列、0 行更新、第二行保存失败、并发修改以及刷新失败；先明确可丢弃数据和写入授权，不以生产库验证文档。

目标框架和依赖以 `ColorVision.Database.csproj` 为准：当前 WPF 多目标 `net8.0-windows7.0;net10.0-windows7.0`，依赖 SqlSugar、SQLite runtime、Newtonsoft.Json、log4net 和 ColorVision.UI。缺 DLL、连接失败、真实库结构不符以及业务结果未完成应分别定位；该模块本身不安装数据库服务。
