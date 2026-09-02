---
knowledge_id: "ui.database-query"
knowledge_type: "topic"
status: "current"
summary: "实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。"
aliases: ["通用查询", "高级查询", "查询窗口", "查询条件保存", "筛选结果", "SQL预览", "清空条件", "清空表", "截断表", "查询取消", "GenericQueryWindow", "GenericQuery", "QueryCondition", "QueryOperator", "GenericQueryConditionSupport", "GenericQuerySessionStore", "GenericQueryBaseConfig"]
code_paths: ["UI/ColorVision.Database/GenericQueryWindow.xaml", "UI/ColorVision.Database/GenericQueryWindow.xaml.cs", "UI/ColorVision.Database/GenericQueryConditionSupport.cs", "UI/ColorVision.Database/GenericQuerySessionStore.cs", "UI/ColorVision.Database/IEntity.cs", "Engine/ColorVision.Engine/Dao/MeasureBatchManagerPage.xaml.cs", "Engine/ColorVision.Engine/Messages/MessagesListManager.cs", "UI/ColorVision.SocketProtocol/SocketMessageManager.cs"]
test_paths: ["Test/ColorVision.UI.Tests/GenericQueryConditionSupportTests.cs"]
related: ["ui.database", "ui.sqlite-storage", "operations.data", "ui.socket-protocol"]
---

# 通用查询、条件会话与整表操作

`GenericQueryWindow` 编辑实体查询条件，并把查询结果写回调用方传入的集合。字段来自 C# 实体，不扫描真实表结构；当前没有分页、后台加载或取消旧结果机制。此窗口继续保留，通用数据库浏览器的移除不改变它的查询和整表操作契约。底层连接见[数据库基础](./ColorVision.Database.md)。

执行查询会访问调用方数据库；默认窗口初始化只装配条件、恢复会话和绑定事件，打开入口是否提前初始化数据库取决于具体调用方。“清空表”和“截断表”是整表写操作。解释源码不授权连接用户库或做清理烟测。维护前必须明确目标表、影响范围、业务停写与恢复方案，不能把当前筛选范围当成删除范围。

## 字段、条件与 SQL

`GenericQuery<T>` 和 `GenericQuery<T,T1>` 都要求实体 `T` 实现 `IEntity`。`GenericQueryConditionSupport.GetQueryableProperties` 选取公共实例可读、非索引属性，排除 `SugarColumn.IsIgnore` 和 `Browsable(false)`；支持字符串、布尔、日期、Guid、枚举、数值及 nullable 形式。字段存在于实体不保证部署数据库具有相同列或类型。

显示名依次取 `Display`、`DisplayName`、内置字段名映射、属性名；排序先按预设字段优先级，再按当前文化的显示名。SQL 列名取 `SugarColumn.ColumnName`，否则使用属性名。条件值参数化不意味着列名经过浏览器 Provider 的同一套标识符校验；实体元数据应由受信代码维护。

| 输入 | 当前行为 |
| --- | --- |
| 多条条件 | 顺次追加 AND；可以重复添加同一字段形成范围。“添加全部”仅补尚未出现的字段，没有 OR 或分组编辑 |
| 字符串 | 默认 LIKE，另有等于/不等于；输入会 Trim；LIKE 包成 `%value%`，用户输入的 `%`、`_` 不转义为字面量 |
| 布尔、枚举 | 下拉选择，提供等于/不等于；枚举传参转换成 Int32，不承诺任意宽整数枚举都可查询 |
| 日期、数值、Guid | 提供比较操作；日期来自 DatePicker，不自动扩展为整天范围；文本转换使用类型转换器和当前文化 |
| 空值或非法值 | 无值行直接跳过，不表示 `IS NULL` 或空串查询；首个非法非空条件显示错误并抛 `FormatException`，没有静默变成零值 |

`ApplyConditions` 用 `@queryValueN` 参数传值，不直接把输入拼入 SQL。窗口 SQL 区绑定 `GenericQueryBase.Sql`，仅在 `QueryDB` 执行到 `ToSqlString()` 时更新，且位于 `Take(Count)` **之前**：它不随编辑实时变化，也不包含稍后添加的数量限制。清空条件不会清掉旧 SQL，数据库执行失败也可能已经显示新 SQL。该字符串还原样进入日志，本层没有脱敏；分享排障信息前检查其中的值。

## 执行、数量限制与结果替换

两个默认泛型实现每次从 `Db.Queryable<T>()` 重建查询，按实体 `Id` 排序。公开的 `Query` 属性没有被默认 `QueryDB` 使用，不能给它赋值后就假设自定义过滤会生效。

`GenericQueryBaseConfig` 默认 `Count = 100`、降序。窗口点击查询时要求数量在 `1..10000`；直接调用 `QueryDB` 没有这一 UI 范围校验，`Count <= 0` 会不加 `Take`。这是一次有上限的查询，不是带总条数的分页协议。

执行顺序为：`PreQuery` → 条件转换/构造查询 → 生成并记录 SQL → 同步 `ToList` → 清空调用方 `ViewResluts` → 逐项加入结果 → `QueryCompleted`。`GenericQuery<T,T1>` 在加入阶段逐项执行 `Converter`；窗口本身不建立独立结果快照。完成事件携带实际取回数量和计时，计时包含集合替换，不是纯数据库耗时；当前窗口完成文案只显示条数。

默认条件转换或数据库查询在清空集合之前失败，通常留下原集合；但 `PreQuery` 订阅者可以先修改状态。转换器、集合事件或完成事件异常可能发生在结果已经清空/部分替换之后。窗口只显示异常，不回滚结果或订阅者副作用，不能概括成“查询失败一定保留旧结果”。

`Query_Click` 虽为 async，但只先 `Dispatcher.Yield` 一次，随后在 UI 线程同步执行查询。没有取消令牌、该层查询超时设置或关窗状态复查：同步阶段可能阻塞窗口；在 yield 间关闭也没有阻止后续查询的判断。关闭按钮的 `IsCancel` 只是窗口关闭语义，不取消 SQL、不回滚结果。

数据库连接由调用方拥有，窗口不负责 `Dispose`；窗口对 query 的事件订阅也没有在关闭时统一解除。复用 query 或延长其生命周期前需核对这些引用，不把关窗当成全部资源已经释放。

## 条件会话保存到哪里

`GenericQuerySessionStore` 是带锁的静态 `Dictionary<Type, GenericQuerySessionState>`。只在当前进程中按实体 `T` 保存条件的属性名、操作符、输入/值，以及数量和排序；不保存数据库连接、SQL、结果行或窗口身份，也不写文件。

- 同一 `T` 的不同连接、不同业务调用方以及两个泛型形态共享最后保存状态，不按 `T1` 或连接隔离；不能作为租户或用户隔离边界。
- 第一次 `GetControl()` 恢复一次，只按当前可查询属性名匹配，已经移除的字段跳过，不在每次显示时重新合并状态。
- 窗口查询正常返回后保存，`Closing` 也保存。因此未执行、甚至无效的条件可能留到下次；进程退出后不保留。
- 数量和排序控件只在点击查询时写入 `QueryConfig`。仅修改这两个控件后关窗，保存的是此前配置值，不是所有屏幕上未应用的编辑。
- `ResetConditions` 清空条件行和该 `T` 的会话记录，但不重置数量、排序、SQL 或调用方结果；之后关闭又会保存一份空条件状态。

## 清空条件不等于清空表

| 动作 | 数据影响与边界 |
| --- | --- |
| 清空条件 | 只重置上述条件状态；不重新查询、不删除记录 |
| 默认 `DeleteAll` | 无条件的 `Db.Deleteable<T>().ExecuteCommand()`；不使用当前条件或数量限制 |
| 默认 `TruncateTable` | 直接发出 `TRUNCATE TABLE {tableName}`；不带当前过滤、不做统一方言适配，也没有浏览器相同的标识符处理 |

窗口的两个整表入口各有 Yes/No 确认；公开方法与 `RelayCommand` 不内置该确认或统一权限检查。操作正常返回后窗口只显示成功，不核对影响行数、不自动重新查询或清空旧结果集合，也不提供备份、统一事务和恢复。不要将所有数据库的截断/自增重置/回滚行为视为相同。

Socket 的实际查询子类用自己的维护锁，并将 SQLite 截断改为事务内 `DELETE` 加 `sqlite_sequence` 清理；这是调用方定制，不是默认 `GenericQuery` 的跨库能力。锁、备份和压缩正文迁移的边界见 [SQLite 存储与维护](./sqlite-storage.md)。

## 实际调用方与验证

- `Engine/ColorVision.Engine/Dao/MeasureBatchManagerPage.xaml.cs`：MySQL 查询实体，`GenericQuery<T,T1>` 转成页面的 `ViewBatchResult` 并替换结果集合，调用方负责连接释放。
- `Engine/ColorVision.Engine/Messages/MessagesListManager.cs`：查询 `MsgRecord` 并替换 `MsgRecords`，在 finally 释放连接；这与 Socket 的库和集合不同。
- `UI/ColorVision.SocketProtocol/SocketMessageManager.cs`：定制查询、SQLite 整表操作及维护锁接入；列表与压缩正文按 ID 读取的责任分开。

`Test/ColorVision.UI.Tests/GenericQueryConditionSupportTests.cs` 覆盖友好字段名与排除项、值解析、布尔与重复范围、空白跳过、直接保存/恢复会话，以及无查询前副作用时非法条件不清旧结果。测试使用临时 SQLite 与 STA 控件构造，引用不表示本次已经运行。

现有专项测试不覆盖真实 Closing、未应用的数量/排序、SQL 数量限制显示、跨连接共享状态、转换器部分失败、关闭/取消时序、整表操作和实际 MySQL。上述执行边界来自源码核对，不是对用户数据库的运行验收；产品修复需单独授权并增加对应测试。
