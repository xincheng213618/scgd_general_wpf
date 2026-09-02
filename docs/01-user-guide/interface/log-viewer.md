---
knowledge_id: "operations.logs"
knowledge_type: "topic"
status: "current"
summary: "区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。"
aliases: ["日志查看器","查看日志","日志搜索","日志等级","历史日志","日志丢失","正则变红","自动刷新","自动滚动","WindowLog","LogOutput","LogLocalOutput","LogLoadState","LogHistoryReader","LogSearchHelper","LogViewerAppender","NativeLogWindow"]
code_paths: ["UI/ColorVision.UI/LogImp","ColorVision/log4net.config","ColorVision/EntryClass.cs","ColorVision/App.xaml.cs","ColorVision/MainWindow.xaml.cs","ColorVision/NativeLogging","UI/ColorVision.Core/NativeLogBridge.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LogHistoryReaderTests.cs","Test/ColorVision.UI.Tests/LogSearchHelperTests.cs","Test/ColorVision.UI.Tests/LogEntryParserTests.cs","Test/ColorVision.UI.Tests/LogViewConfigTests.cs","Test/ColorVision.UI.Tests/NativeLogPendingBufferTests.cs","Test/ColorVision.UI.Tests/NativeLogWindowTests.cs","Test/ColorVision.UI.Tests/ContextualFindRouterTests.cs","Test/ColorVision.UI.Tests/FeedbackLogCollectorTests.cs"]
related: ["operations.index","ui.framework","ui.configuration","ui.core"]
---

# 日志来源、历史读取与筛选

`UI/ColorVision.UI/LogImp/` 负责托管日志显示、历史解析和搜索；业务模块产生的日志、文件输出与当前可见文本是不同层。界面为空、被清空或没有某条记录，都不能单独证明设备命令、文件写入或流程动作没有发生；完成判据仍应核对[对应模块的执行契约](../README.md)。

## 日志从哪里来

| 入口 | 数据来源与边界 |
| --- | --- |
| `WindowLog`，帮助菜单的日志窗口（快捷键可配置，默认 Ctrl+Alt+L） | 给 log4net 根 logger 附加 `LogViewerAppender` 接收新事件，并在初始化时读取当前文件历史；每次菜单执行创建新窗口 |
| `LogOutput`，主窗口等嵌入式面板 | 同样接收 log4net 新事件，本身不加载文件历史；`ModuleLogViewerBinder` 的模块面板只接受指定 logger 名或其点分隔子名称 |
| `WindowLogLocal` / `LogLocalOutput` | 读取调用方指定的外部文件，不接收 log4net 事件；编码可由调用方指定，默认 `Encoding.Default` |
| `NativeLogWindow` | 独立的 `NativeLogBridge` 回调与采集会话，不是从托管日志文件读回 native 历史 |

主程序通过 `EntryClass.cs` 的 `XmlConfigurator` 加载并监视 `ColorVision/log4net.config`。当前配置包含控制台和按日期滚动的 UTF-8 文件输出，文件配置前缀为 `log\`；当入口检查到当前工作目录包含 `C:\Program Files` 时，会改到应用数据目录下的 `ColorVision\Log\`。定位应读取当前 appender 的 `File`，不能只猜安装目录或将主面板当作完整日志文件。

## 反馈打包的应用日志范围

`AppLogCollector` 同时收集 `%APPDATA%\ColorVision\Log`、当前程序安装目录下的 `log`，以及当前文件 appender 所在目录。安装目录从 `AppDomain.CurrentDomain.BaseDirectory` 获取，覆盖安装在 `C:\Program Files` 下的程序，不扫描其他软件目录。更新后重启和正常启动可能选择不同日志目录，反馈不能只收当前正在写入的位置。

目录转为绝对路径、忽略大小写和末尾分隔符后去重。ZIP 中分别使用 `AppLogs/AppData/`、`AppLogs/Installation/` 和 `AppLogs/Current/`，保留不同目录内同名的日志；当前目录与前两者重合时只收一次。所有来源沿用所选时间范围（默认最近 7 天）、按 `LastWriteTimeUtc` 过滤、单文件不超过 50 MiB 和只读顶层文件的规则。缺失目录会跳过，某个目录无法读取或某个文件复制失败不会阻止收集其他来源。“打开日志目录”仍指向当前 appender 所在目录，打包范围更广。

`FeedbackLogCollectorTests` 使用临时目录覆盖两处同名日志同时保留、当前目录去重、自定义当前目录、时间范围和其他目录缺失时仍收集安装目录日志；不依赖现场管理员权限或生产日志。

## 全局输出等级不是本地筛选

`WindowLog`、`LogOutput` 右键菜单中的“日志等级”调用 `LogConfig.LogLevel`，进而由 `SetLog()` 修改 `Hierarchy.Root.Level`。这会影响继承根等级的后续 log4net 事件及其文件/控制台/UI 输出，不会重新筛选已显示的历史，更不能补回此前没有产生的低等级事件。配置文件初始为 `All`，但主程序初始化配置后会调用 `LogConfig.Instance.SetLog()`；该配置默认 `Info`，实际值还取决于已保存配置。

若只想缩小当前可见范围，应使用搜索；不要为了看得清而把提高全局等级当作无副作用的显示操作。改变等级、开启真实采集或重现业务动作，应属于当前任务明确授权的排障范围。

配置也不是所有窗口各自隔离：`WindowLog` 使用 `LogConfig.Instance`，主面板显式使用独立的 `LogPanelConfig.Instance`，默认构造的 `LogOutput` 使用新的 `RealtimeLogViewConfig`。外部文件视图共享 `WindowLogLocalConfig`，且其模式、字符上限和颜色仍绑定 `LogConfig.Instance`。持久化规则见[配置注册、保存与恢复](../../04-api-reference/ui-components/configuration.md)。

## 历史读取、时间与大小限制

### 托管日志窗口

`WindowLog.LoadLogHistory()` 只取根 logger 上第一个 `RollingFileAppender.File`，以 `FileShare.ReadWrite` 打开当前文件，不遍历滚动归档。`LogLoadState` 默认 `SinceStartup`：

- `SinceStartup`：保留时间戳不早于当前进程启动时间的记录。
- `AllToday`：保留时间戳日期等于本机当天日期的记录；不是读取所有历史文件。
- `None`：跳过此次历史读取，不禁止实时事件；该窗口实时刷新初始为开启。

`LogHistoryReader` 逐行扫描，以行首精确格式 `yyyy-MM-dd HH:mm:ss,fff` 划分记录；后续无时间戳行并入前一条，文件开头没有可识别时间戳的行不会进入这条历史读取链。倒序按整条记录反转，不反转异常堆栈内部行。初始化读取是同步的；字符上限不是文件字节上限，也不是只扫描文件尾部的保证。文件不存在时不会加载历史；读取异常弹窗提示，不会证明实时 appender 也失效。

`MaxChars` 默认 `-1`，当前实现只有值大于 `1000` 才裁剪。历史读取先淘汰较旧整条记录，`ReadEntries` 至少保留一条，因此单条超长记录仍可越限；TextBox 路径最后还会硬截字符，可能截断一条记录。`MaxEntries` 是控件条目集合上限：完整窗口默认 `10000`、实时配置默认 `1000`，非正数不限；TextBox 历史由 `SetText` 写入，不能用条目上限承诺初次历史读取已受限。虚拟化模式也不是完整日志归档或严格内存上限。

### 外部日志文件

`LogLocalOutput` 初次读取默认保留末尾 `1000` 行，再按记录解析；`MaxLines <= 0` 才读全量。适用编码使用向后查找换行的尾读，UTF-16/UTF-32 等情形退回从头读取。限制的是物理行，边界可能切掉一条多行记录的开头，不保证整条异常都在视图中。

自动刷新通过定时器（默认 `500 ms`）和可用时的 `FileSystemWatcher` 跟踪新内容。读取位置是文件字节位置：长度增长才增量读取，变短则重新加载；同长度替换、截断后快速长回旧长度的轮转不能据此保证识别。初次文件缺失或读取失败会在视图显示消息；后续增量读取异常被捕获且不显示，因此“没有新日志”也可能是读取失败。手动刷新重新读取文件，不是重启日志源。

## 刷新、滚动、清空与生命周期

- 托管 `LogViewerAppender` 默认约每 `100 ms` 将缓冲调度到 UI；刷新间隔可配置。关闭其自动刷新会直接跳过这段时间的新 UI 事件，并非无损暂停队列；重新开启只接收之后的事件，不自动补读文件。文件输出是否继续取决于日志源与文件 appender，不由 UI 刷新开关决定。
- 外部文件的自动刷新暂停的是读取，读取位置保留；恢复后可以读尚在同一文件中的追加内容，仍受轮转与显示裁剪限制。不要把这与托管事件跳过或 native 暂停混为一谈。
- `AutoScrollToEnd` 只决定追加后的视口跟随，不控制日志产生、接收或落盘；鼠标选择/滚轮会临时暂停跟随，约两秒后解除暂停。关闭自动滚动也不防止旧内容被容量裁剪。
- “清空”只清除控件文本、条目和选择，不删除源文件、不停止业务或全局日志，也不等于清空 appender 尚待刷新的缓冲。复制只取得当前显示中选中的内容，不是完整日志导出；本页这些查看控件没有自动保存当前视图的契约。
- `LogOutput` 在加载/卸载时挂接/移除 appender，`Dispose` 另解除事件与搜索控制器；`WindowLog` 关闭时移除并释放 appender。模块 binder 也必须释放，否则仍订阅根 logger。`LogLocalOutput` 卸载时停止定时器/监视器，最终 `Dispose` 才解除配置订阅并释放监视器，独立窗口关闭会调用它。

`LogViewerControl` 默认 TextBox；Virtualized 使用条目视图与等级颜色。模式在控件第一次 `Loaded` 后锁定，修改配置需新建相应控件/窗口才能切换。视图中被裁掉的内容不能靠清空搜索恢复；要追溯应按权限读取仍保留的源文件。

## 搜索与失败语义

托管/外部文件视图通过右键“搜索”或 `Ctrl+F` 打开搜索栏；`Esc` 或关闭按钮清空搜索并隐藏栏。输入有约 `200 ms` 防抖，清空立即应用。不要套用其他模块顶部搜索框的响应式隐藏规则。

`LogTextViewController` 同时在所属键盘目标上注册标准 `ApplicationCommands.Find`，执行仍是显示同一搜索栏；Detach 时解除命令绑定。这样主窗口可配置的[场景查找](../../04-api-reference/ui-components/hotkeys.md)能直接复用当前日志视图的局部搜索，而不先打开应用功能搜索，也不模拟 Ctrl+F。此适配不改变过滤算法、日志源或全局日志等级。

- 普通搜索按空格拆词，每个词必须在同一被匹配文本中出现，忽略大小写。TextBox 按行过滤，Virtualized 按整条 `LogEntry.Text` 过滤，因此多行异常的匹配和保留范围会不同。
- 含 `. * + ? ^ $ ( ) [ ] { } | \` 中任一字符就自动转为正则；不存在独立的“纯文本/正则”开关，文件名中的点和 Windows 路径也会触发。正则使用 `IgnoreCase`，每次匹配超时为 `250 ms`，不是全量搜索总时限。
- UI 的 `NormalizeSearchText` 会先按当前文化转成小写，再传给 helper；不能承诺任意正则原样执行，例如 `\D` 会变为 `\d`。复杂模式需核对这条调用链，不能只看 helper 测试。
- 正则解析失败或匹配超时时返回 `false`；直接应用搜索时边框变红，控件保留原可见结果，而不是显示“匹配为零”。无效搜索串仍是当前搜索条件，后续追加可能继续筛选失败；先修正或清空再判断日志是否缺失。

`LogSearchHelper` 仅缓存最近的有效正则及无效模式，不缓存完整文件或历史搜索结果。控件另保留原始条目/文本与可见结果，清空搜索只能恢复当前仍在内存中的内容。

## Native 采集是另一条控制链

`NativeLogWindowService` 复用已打开的 native 窗口，窗口会话初始不采集；“开始”才调用 `NativeLogCaptureController.Start` 初始化/启用可用 native 日志源，可能加载 DLL。其等级影响进程级 `NativeLogBridge`，不等于 log4net 根等级或本地筛选。当前控制器传入 `sink: null`、`enableNativeSink: false`，不能据此承诺保存到托管日志文件；桥接层允许其他调用方提供 sink，但该窗口不安装文件输出。

native 的“暂停”仅暂停显示队列取出，采集仍继续。待显示缓冲上限 `8192`，溢出丢最旧记录并累加 `Dropped`；每次刷新最多取 `512` 条。停止或关闭窗口会请求禁用 bridge，而清空会清待显示缓冲、丢弃计数和当前视图。这些操作都不是停止算法/设备的命令。实际采集需先确认授权，排障文档不授权启动产品或触发硬件。

## 证据与验证缺口

- `LogHistoryReaderTests` 用合成文本验证启动时间边界、续行与顺/倒序字符裁剪；`LogEntryParserTests` 验证部分等级识别和多行归组。
- `LogSearchHelperTests` 验证普通词 AND、正则、非法语法、行/对象匹配；没有据此证明 UI 防抖、变红、超时后的可见状态或大小写规范化都已验证。
- `LogViewConfigTests` 验证主面板与实时实例的默认条目限制和配置独立性，包含 STA 构造检查；不等于验证刷新丢弃、关闭释放或大文件性能。
- `ContextualFindRouterTests` 使用独立控件与日志搜索控制器检查标准 Find 显示搜索栏及 Detach 解绑；不挂接生产日志 appender，不读取现场日志。
- `NativeLogPendingBufferTests` 验证容量淘汰、批量读取、清空和 fake controller 会话暂停；`NativeLogWindowTests` 只验证菜单与编译 XAML 存在，不是 native DLL 或真实窗口交互验收。

当前主题未声明外部文件轮转/编码组合、实际文件写入保留、UI 生命周期压力或生产日志不丢失的端到端覆盖。最小人工复核应使用获准的合成日志与隔离环境；不为文档校验开启真实采集。对外分享前脱敏路径、客户标识、设备数据与凭据，不删除现场日志来“清理”排障证据。
