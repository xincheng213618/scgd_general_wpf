---
knowledge_id: "ui.database-query"
knowledge_type: "topic"
status: "current"
summary: "实体驱动的通用查询窗口，支持参数化筛选、调用方固定范围、前后台查询和只读模式。可写模式的表维护作用于整表。"
aliases: ["通用查询", "高级查询", "查询窗口", "查询条件保存", "筛选结果", "SQL预览", "清空条件", "清空表", "截断表", "查询取消", "GenericQueryWindow", "GenericQuery", "QueryCondition", "QueryOperator", "GenericQueryConditionSupport", "GenericQuerySessionStore", "GenericQueryBaseConfig"]
code_paths: ["UI/ColorVision.Database/GenericQueryWindow.xaml", "UI/ColorVision.Database/GenericQueryWindow.xaml.cs", "UI/ColorVision.Database/GenericQueryConditionSupport.cs", "UI/ColorVision.Database/GenericQuerySessionStore.cs", "UI/ColorVision.Database/IEntity.cs", "Engine/ColorVision.Engine/Messages/MessagesListManager.cs", "UI/ColorVision.SocketProtocol/SocketMessageManager.cs"]
related: ["ui.database", "ui.sqlite-storage", "operations.data", "ui.socket-protocol"]
---

# 通用查询、条件会话与整表操作

“高级查询”（`GenericQueryWindow`）用于按实体字段筛选业务列表。直接在条件行中选择字段、比较方式并填写值，设置数量和排序，再点击“查询”，结果会替换原业务列表；窗口不另建结果表，也不内置业务专属查询方案。

字段来自 C# 实体，不扫描真实表结构；当前没有分页、后台加载或取消旧结果机制。默认窗口初始化只装配条件、恢复会话和绑定事件，查询时才访问调用方数据库；打开入口是否提前初始化数据库取决于具体调用方。底层连接见[数据库基础](./ColorVision.Database.md)。

## 字段、条件与 SQL

`GenericQuery<T>` 和 `GenericQuery<T,T1>` 都要求实体 `T` 实现 `IEntity`。`GenericQueryConditionSupport.GetQueryableProperties` 选取公共实例可读、非索引属性，排除 `SugarColumn.IsIgnore` 和 `Browsable(false)`；支持字符串、布尔、日期、Guid、枚举、数值及 nullable 形式。字段存在于实体不保证部署数据库具有相同列或类型。

显示名依次取 `Display`、`DisplayName`、内置字段名映射、属性名；排序先按预设字段优先级，再按当前文化的显示名。SQL 列名取 `SugarColumn.ColumnName`，否则使用属性名。条件值参数化，但列名直接来自实体元数据，本层没有额外的标识符校验；实体元数据应由受信代码维护。

没有恢复条件时，窗口直接提供一条待填写的条件行。字段选择器同时展示友好名称和属性名，输入可按显示名、属性名或数据库列名进行包含搜索；每一行独立筛选候选。Enter 确认唯一候选或当前选项后转到值，不同时触发查询。换字段会清除旧值和错误、恢复该类型的默认比较方式，并换成对应的文本、日期、布尔或枚举控件。输入了字段文字但尚未选中候选时，查询会报错，不沿用旧字段或静默忽略该行；完全空白的待填写行不参与过滤。

| 输入 | 当前行为 |
| --- | --- |
| 多条条件 | “添加条件”追加待填写行，顺次追加 AND；可以重复选择同一字段形成范围。更多菜单中的“全部添加”仅补尚未出现的字段，没有 OR 或分组编辑 |
| 字符串 | 默认 LIKE，另有等于/不等于；输入会 Trim；LIKE 包成 `%value%`，用户输入的 `%`、`_` 不转义为字面量 |
| 布尔、枚举 | 下拉选择，提供等于/不等于；枚举传参转换成 Int32，不承诺任意宽整数枚举都可查询 |
| 日期、数值、Guid | 提供比较操作；日期来自 DatePicker，不自动扩展为整天范围；文本转换使用类型转换器和当前文化 |
| 空值或非法值 | 无值行直接跳过，不表示 `IS NULL` 或空串查询；首个非法非空条件显示错误并抛 `FormatException`，没有静默变成零值 |

`ApplyConditions` 用 `@queryValueN` 参数传值。查询执行时在 `Take(Count)` 之前调用 `ToSqlString()` 生成预览和日志。窗口绑定 `GenericQueryBase.Sql`，默认同步查询在执行 SQL 前更新，后台查询在提交结果时更新。分享 SQL 预览或日志前检查其中的值。

## 执行、数量限制与结果替换

两个默认泛型实现每次从 `Db.Queryable<T>()` 重建查询，按实体 `Id` 排序。调用方可为 `GenericQuery<T,T1>` 设置 `CreateBaseQuery`，在用户条件、排序和限额之前固定查询范围。公开的 `Query` 属性仍用于调用方自行组织的查询。

`GenericQueryBaseConfig` 默认 `Count = 100`、降序。窗口点击查询时要求数量在 `1..10000`；直接调用 `QueryDB` 没有这一 UI 范围校验，`Count <= 0` 会不加 `Take`。这是一次有上限的查询，不是带总条数的分页协议。

执行顺序为：`PreQuery` → 条件转换/构造查询 → 生成并记录 SQL → 同步 `ToList` → 清空调用方 `ViewResluts` → 逐项加入结果 → `QueryCompleted`。`GenericQuery<T,T1>` 默认逐项执行 `Converter`；指定 `BatchConverter` 时，每次查询改为一次传入已排序、限额后的全部行，调用方可批量补充关联数据；窗口本身不建立独立结果快照。完成事件携带实际取回数量和计时，计时包含转换与集合替换，不是纯数据库耗时；当前窗口完成文案只显示条数。

默认条件转换或数据库查询在清空集合之前失败，通常留下原集合；但 `PreQuery` 订阅者可以先修改状态。转换器、集合事件或完成事件异常可能发生在结果已经清空/部分替换之后。窗口只显示异常，不回滚结果或订阅者副作用，不能概括成“查询失败一定保留旧结果”。

检测结果历史页的批次搜索是调用方自己的异步路径。查询失败时它保留上一次成功加载的行，同时在 Copilot `measurement-results` 上下文中明确标记 `Last query: Failed`、`Stale (last query failed; loaded rows may predate it)`、本次请求筛选是否仍与已加载筛选一致，以及上次成功加载的 UTC 时间。这样保留的旧行不会被当成本次筛选结果；这些新鲜度字段也用于高级查询失败后的不可信列表，不改变通用 `GenericQueryWindow` 的部分替换边界。

`Query_Click` 默认先让界面显示查询状态，再在界面线程同步执行查询。关闭窗口会取消尚未开始的查询。

光谱与校准的高级查询通过 `GenericQuery<T,T1>.BackgroundQueryClientFactory` 启用后台查询。点击查询时界面固定筛选值、排序、数量和转换器。后台使用工厂创建本次查询的客户端，读取并转换整批结果后释放客户端。转换成功且查询仍有效时，界面替换集合并发出 `QueryCompleted`。调用方可通过 `CanApplyBackgroundResults` 在提交前复核设备与结果来源。关闭窗口取消结果提交，正在执行的同步 SQL 或文件读取在后台返回后完成清理。

传给查询对象的 `Db` 由调用方释放。窗口关闭时解除自己订阅的条件变更和查询完成事件。

## 条件会话保存到哪里

`GenericQuerySessionStore` 是带锁的静态 `Dictionary<Type, GenericQuerySessionState>`。只在当前进程中按实体 `T` 保存条件的属性名、操作符、输入/值，以及数量和排序；不保存数据库连接、SQL、结果行或窗口身份，也不写文件。

- 同一 `T` 的不同连接、不同业务调用方以及两个泛型形态共享最后保存状态，不按 `T1` 或连接隔离；不能作为租户或用户隔离边界。
- 第一次 `GetControl()` 恢复一次，只按当前可查询属性名匹配，已经移除的字段跳过，不在每次显示时重新合并状态。
- 窗口查询正常返回后保存，`Closing` 也保存。只保存已选定字段的条件，空行或未确认的字段搜索文字不保存；已选字段的未执行、甚至无效值仍可能留到下次。进程退出后不保留。
- 数量和排序控件只在点击查询时写入 `QueryConfig`。仅修改这两个控件后关窗，保存的是此前配置值，不是所有屏幕上未应用的编辑。
- `ResetConditions` 清空条件行和该 `T` 的会话记录，但不重置数量、排序、SQL 或调用方结果。窗口的“清除条件”随后补一条待填写行，关闭时仍保存为空条件状态。

## 清空条件不等于清空表

调用方可用 `IsReadOnly` 启用只读模式。校准设备的高级查询使用此模式，隐藏整表操作，并在泛型查询对象的方法和命令入口禁用清空及截断。

| 动作 | 数据影响与边界 |
| --- | --- |
| 清空条件 | 只重置上述条件状态；不重新查询、不删除记录 |
| 默认 `DeleteAll` | 无条件的 `Db.Deleteable<T>().ExecuteCommand()`；不使用当前条件或数量限制 |
| 默认 `TruncateTable` | 直接发出 `TRUNCATE TABLE {tableName}`；不带当前过滤；本层不做统一方言适配或表名转义 |

整表操作前应明确目标表、影响范围、业务停写与恢复方案。窗口的两个整表入口位于“更多 → 表维护”，各有 Yes/No 确认；公开方法与 `RelayCommand` 不内置该确认或统一权限检查。操作正常返回后窗口只显示成功，不核对影响行数、不自动重新查询或清空旧结果集合，也不提供备份、统一事务和恢复。不要将所有数据库的截断/自增重置/回滚行为视为相同。

Socket 查询在自己的维护锁内执行整表 `DELETE`，并保留消息 ID 的自增序列。该入口只显示删除菜单，旧的程序调用也沿用安全删除行为。锁、备份和压缩正文迁移的边界见 [SQLite 存储与维护](./sqlite-storage.md)。

## 实际调用方与验证

- `Engine/ColorVision.Engine/Messages/MessagesListManager.cs`：查询 `MsgRecord` 并替换 `MsgRecords`，在 finally 释放连接；这与 Socket 的库和集合不同。
- `UI/ColorVision.SocketProtocol/SocketMessageManager.cs`：定制查询、SQLite 整表操作及维护锁接入；列表与压缩正文按 ID 读取的责任分开。

核对字段搜索隔离、Enter 查询门槛、关闭后的配置保存及深浅主题下的布局。

在获授权环境核对真实 MySQL 连接、整表操作及失败后的状态。
