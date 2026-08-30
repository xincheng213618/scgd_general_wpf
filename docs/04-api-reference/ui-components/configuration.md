---
knowledge_id: "ui.configuration"
knowledge_type: "topic"
status: "current"
summary: "ConfigHandler的配置路径、延迟实例、文件合并保存和重载契约；单文件替换不等于内存发布成功，重载会使旧配置引用失效。"
aliases: ["配置文件在哪里","改了配置文件为什么没生效","配置导出缺少项目","配置重载旧引用","保存后发布失败","ConfigHandler","ConfigService","SaveConfigs","Reload","ReloadFromDisk","ConfigsReloaded","TrySaveAndPublish","PersistedButPublishFailed","IConfigSecure"]
code_paths: ["UI/ColorVision.UI/ConfigHandler.cs","UI/ColorVision.Common/Interfaces/Config/IConfig.cs","UI/ColorVision.Common/Interfaces/Config/IConfigSecure.cs","UI/ColorVision.Common/Interfaces/Config/IConfigService.cs","UI/ColorVision.Common/Interfaces/Config/ConfigService.cs","UI/ColorVision.UI/ConfigSetting/ConfigServiceAdapters.cs","UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ConfigHandlerPersistenceTests.cs","Test/ColorVision.UI.Tests/ConfigServiceAdaptersTests.cs"]
related: ["ui.framework","ui.settings","ui.wizards","ui.menus","ui.property-grid","operations.exports","operations.device-configuration"]
---

# 配置持久化、重载与对象所有权

`ConfigHandler` 负责应用设置 JSON 的读取、实例缓存、合并写入和备份恢复；`ConfigService` 只是可替换的 `IConfigService` 入口。文件保存、运行期对象发布和订阅者重绑定是不同完成条件，不能用“保存方法返回”统一代表。

本主题不处理设备资源的 MySQL 保存，也不定义属性编辑事务：分别见[设备资源配置](../../01-user-guide/devices/configuration.md)和[属性编辑器契约](./property-grid.md)。设置导入/导出界面的调用顺序与覆盖风险由[导入导出边界](../../01-user-guide/data-management/export-import.md)负责。

## 配置路径由谁决定

`ConfigHandler.GetInstance(name)` 第一次创建实例时调用 `Load`，初始化路径并注册 `ConfigService.Instance`；后续即使传入不同名称，也返回既有实例，不切换配置文件。仅 `new ConfigHandler()` 不会自动执行 `Load`。

`InitializePaths` 默认文件名前缀为入口程序集名加 `Config`，显式 `ConfigDIFileName` 可替代这个前缀：

| 条件 | 主文件与备份目录 |
| --- | --- |
| 当前工作目录中已存在相对 `Config` 目录 | `Config/<ConfigDIFileName>.json` 与 `Config/Backup` |
| 不存在该相对目录 | `ApplicationData/<入口程序集公司名>/Config/<ConfigDIFileName>.json` 与其 `Backup` 子目录；公司名缺失时使用程序集名 |

最终路径用 `Path.GetFullPath` 规范化。这里判断的是工作目录，不是可执行文件所在目录；启动方式改变工作目录可能选择另一份配置。`Load` 会创建必要目录，且坏文件加载可能触发备份恢复写回，所以首次获取单例不是保证无副作用的只读查询。

`Load` 时若 `IsAutoSave=true`，会安排一次约两分钟后的备份；不是持续的周期保存。退出处理器会注册，并在退出时检查 `IsAutoSave` 决定是否保存。关闭该标志不会禁止显式保存/备份，也不会取消已安排的延迟备份。

## 配置类型、延迟构造与适配器

`IConfig` 是标记接口，不要求配置类实现 `Load` / `Save`。`ConfigHandler` 用 `ConcurrentDictionary<Type,IConfig> Configs` 缓存已取得的对象；读取正常 JSON 后，先保留 `JObject`，等 `GetRequiredService(type)` 才按需反序列化。

内存键是 `Type`，JSON 节名却是 `type.Name`，不是完整命名空间。两个同名配置类型会指向相同 JSON 节；类型改名也不会自动迁移旧节。缺少该节或反序列化/解密失败时尝试无参默认构造，失败的反序列化会记日志；默认构造自身失败仍可能抛出。

`ConfigService.SetInstance` 只替换服务引用，不加载文件或迁移旧对象。不同实现不能混用持久化假设：

| 实现 | 对象解析责任 | 文件保存/加载 |
| --- | --- | --- |
| `ConfigHandler` | 按类型缓存，从 JSON 或默认构造取得对象 | 实现本页文件契约 |
| `SelfManagedConfigServiceAdapter` | 延迟读取公开静态 `Instance` 并缓存 | `SaveConfigs`、`LoadConfigs`、`Save<T>` 均抛 `NotSupportedException` |
| `HybridConfigServiceAdapter` | 显式注册优先，否则解析静态 `Instance`；重新注册会清除此类型解析缓存 | 同上，不实现持久化 |
| `AspNetCoreConfigServiceAdapter` | 委托 `IServiceProvider.GetService`，生命周期由外部容器决定 | 同上，不实现持久化 |

两个静态实例适配器的缓存是普通字典，不具备 `ConfigHandler` 的保存事务机制。不要因接口相同就推断全部实现都支持配置落盘或重载通知。

## 重载会替换对象，不是原地更新

`ApplyLoadedConfig` 替换内部 `jsonObject`，并把整个 `Configs` 换成新的空字典。之后取得的配置对象是新实例；旧对象、其集合和事件订阅不会自动迁移。

| 入口 | 行为与失败语义 |
| --- | --- |
| `Reload()` | 先 `SaveConfigs`，再 `LoadConfigs`；可能先把旧内存值覆盖到外部修改过的文件，不是纯粹接纳磁盘修改 |
| `ReloadFromDisk()` | 不先保存；读失败则抛错，在文件验证阶段保留已加载对象；成功后替换缓存并额外重绑 `Authorization.Instance` |
| `LoadConfigs(fileName)` / 无参版本 | 加载指定文件或主文件；缺失/损坏时进入备份/默认回退；指定文件名不改变后续保存使用的 `ConfigFilePath` |
| `LoadDefaultConfigs()` | 名称虽是“默认”，仍先尝试最新有效备份；本方法自己不发布 `ConfigsReloaded`，异常记录后吞掉 |

普通 `LoadConfigs` 不负责重绑 `Authorization.Instance`；启动 `Load` 和 `ReloadFromDisk` 分别另做这一步。不能把重载理解为所有静态对象已经刷新。

`LoadConfigs` / `ReloadFromDisk` 在状态替换和事务结束后同步直接调用 `ConfigsReloaded`。它不是统一的 UI Dispatcher 派发，也没有逐订阅者异常隔离；一个订阅者抛错可能阻止后续订阅者，且已替换的缓存不会因此回滚。订阅者应重新取得配置，解除旧对象订阅，并在自身线程/生命周期边界处理未结束工作；保留旧引用的模块仍可能继续使用旧值。

`ConfigSettingManager` 的设置元数据也缓存 `Source` 对象。`InvalidateCache` 只清设置项缓存，不清程序集类型缓存，也不会自动改写已打开控件的绑定。配置服务本身不自动替所有使用方调用该方法；窗口发现、搜索和页面复用见[设置窗口](./settings.md)，具体编辑器和导入链分别见[属性编辑](./property-grid.md)、[导入导出](../../01-user-guide/data-management/export-import.md)。

## 保存是合并目标文件，不是完整内存镜像

`SaveConfigs(fileName)` 先序列化 `Configs.ToArray()` 中已实例化的配置，再在锁内重新读取目标文件，把这些节覆盖到目标 JSON。未被覆盖的目标节保留，但 `ConfigOptions` 和 `MarketplaceServiceConfig` 两个明确过期节会移除。

这带来三个边界：

- 保存主文件时，未实例化节通常由现有主文件保留；并非保存前自动实例化所有配置。
- 导出或备份到新文件时，没有既有目标节可合并，所以不会自动复制原 `jsonObject` 内所有未实例化节。
- 保存到已有其它文件时，可能保留那个目标里的旧节；不是“精确替换为当前内存所有内容”。

任一已实例化节序列化失败，`CreateConfigSnapshot` 汇总错误并抛出，不写部分成功节。已有目标文件若不是一个完整 JSON 对象，保存会拒绝覆盖；尾随其它内容也视为无效。目标不存在时才从空对象开始。

`TrySave<T>(candidate)` 只把 `typeof(T).Name` 对应节合并到主文件，保留其它目标节；它既不把 candidate 自动注册进 `Configs`，也不更新内部 `jsonObject`。另建候选对象保存成功后，已有缓存可能仍旧；尚未实例化的类型也可能继续从旧加载快照取值。运行期发布应由调用方明确完成，不能假设保存方法已重绑所有消费者。

## 单文件写入、锁和事务版本

`WriteConfigFile` 在目标同目录创建独占临时文件，写入 UTF-8 JSON，刷新写入及磁盘，重新读取验证后，已有目标用 `File.Replace`，新目标用 `File.Move` 提交。提交前的序列化/写入失败不会先截断旧文件；临时文件在 `finally` 尝试清理，清理失败只记警告。`File.Replace` 没有创建备份文件，历史备份是独立机制。

互斥分为两层：

- 每个 handler 的保存状态锁和 `_saveTransactionVersion` 排序保存/重载。全量快照在事务外生成；期间若本 handler 保存或重载已提交，版本不符就重新取快照，避免写入已失效快照。
- 文件锁用规范化绝对路径的大写形式计算 SHA-256，取得名为 `Local\ColorVision.ConfigSave.<hash>` 的命名 Mutex，等待上限 30 秒；废弃 Mutex 视为成功取得。不同 handler 针对同一路径的读—合并—写可由它串行化。

这里的版本是 handler 内部事务计数，不是配置 schema 版本或跨进程乐观锁。普通属性赋值、其它 handler 的内存变化或绕过此实现的直接文件写入不受该计数保护；`Local` Mutex 也不是跨会话/跨机器锁。`ConcurrentDictionary` 不会把所有配置对象和集合变成线程安全快照。

序列化会经 `Application.Current.Dispatcher` 同步执行；没有应用 Dispatcher 时在当前线程执行。保存不仅是后台文件 I/O。文件替换的原子提交不等于内存对象图、多个文件和外部副作用的一次原子事务。

## IConfigSecure 的边界

加载成功反序列化的 `IConfigSecure` 对象时调用 `Decrypt`；失败会记录并回退默认实例。保存时先把 candidate 序列化/反序列化为副本，只对副本调用 `Encryption`，再序列化副本写盘。

因此加密钩子失败不应把原 candidate 留在半加密状态；已有测试针对这一点断言。但接口只是钩子，不证明实现采用了何种密钥管理或保护了每个字段，也不保证配置构造/属性访问完全无副作用。不能把所有 JSON 默认当作已加密，更不要把实际配置内容复制进日志或知识文档。

## 落盘、内存发布与回滚

`TrySaveAndPublish(candidate, onPersisted, out error)` 先完成文件提交、释放文件 Mutex，再调用同步 `Action onPersisted`；handler 保存事务在发布结束后才结束。这个回调由调用者负责内存更新/通知，不是自动的 `ConfigsReloaded`。

| 返回状态 | 已完成什么 | 未自动完成什么 |
| --- | --- | --- |
| `NotPersisted` | 本次写盘未成功，错误通过 `error` 返回 | 不撤销调用者此前对活对象的修改 |
| `PersistedAndPublished` | 文件提交成功，所提供的发布回调正常返回 | 不证明每个业务消费者已生效，也不等待回调之外的异步工作 |
| `PersistedButPublishFailed` | 文件已提交，发布回调抛错 | 不回滚磁盘；回调已做的一部分内存更新也不自动撤销 |

发布阶段以 `AsyncLocal` 标记防止同一 handler 重入保存，继承该执行上下文的任务也会被拒绝；不要在发布回调里再触发保存。文件 Mutex 已释放，其它 handler 此时仍可提交自己的写入，不能把“落盘 + 发布”当作全局隔离事务。

`TrySave` 不传发布回调，返回 `true` 只证明这次持久化路径成功。`Save<T>()` 取得缓存对象后调用 `TrySave`，丢弃布尔结果和错误信息；它正常返回不证明文件保存成功。`SaveConfigs` 则直接传播序列化/写入错误。处理故障时先区分“写盘失败”“已写盘但发布失败”“保存包装方法吞掉结果”，不能统一声称已经回滚。

## 备份、损坏文件与导入范围

`BackupConfigs` 使用 `<ConfigDIFileName>Backup_yyyyMMdd_HHmmss.json`，通过 `SaveConfigs(backupPath)` 保存当前已实例化快照；它不是直接复制主文件。清理目标是按文件名倒序保留最多 10 个匹配备份文件，并不先筛出有效 JSON。备份和清理异常分别记日志，调用方拿不到可靠的布尔成功结果。

普通加载在文件缺失或 `TryReadConfigFile` 返回失败时，按备份文件名倒序寻找可解析的 JSON，跳过坏备份；找到后写回主 `ConfigFilePath` 并替换内存。取得文件锁等更早的异常仍会向外抛出，不保证进入回退。没有可用备份，或恢复尝试异常时，回退为空 `jsonObject` 并扫描当前程序集默认构造配置；这条默认回退不同于正常加载后的延迟实例化，类型扫描/构造失败会被跳过。

默认回退不会保证已修复损坏的主文件；若坏文件仍在，后续保存仍可能被“拒绝覆盖无效 JSON”的保护拦下。`ReloadFromDisk` 不使用这套自动恢复，它在读文件失败时拒绝重载。

设置文件导入不是数据库、插件和全部资源的恢复；导入界面是否先备份、如何覆盖文件、失败后是否补偿，统一见[设置导入导出契约](../../01-user-guide/data-management/export-import.md)。此处的单文件提交不能作为导入全过程具有回滚保证的证据。

## 验证入口与缺口

`ConfigHandlerPersistenceTests` 覆盖重载对象替换及通知顺序、坏文件拒绝、同/不同 handler 并发保存保留其它节、旧快照重试、落盘/发布失败区分、同步与异步上下文重入拒绝、写入/序列化失败不改旧字节、加密失败不改 candidate，以及有效备份回退。这些不同 handler 测试仍运行在同一测试进程，不等于真实多进程故障验收。

`ConfigServiceAdaptersTests` 覆盖静态实例解析、显式注册优先/替换、容器解析和错误。名称含 `ConfigSettingManager_WorksWith...` 的用例只模拟其 `GetRequiredService` 调用，不是完整设置页面发现、重载或绑定集成测试。

测试引用不代表已执行。工作目录切换、未实例化节的导出完整性、订阅者抛错后的其它消费者、跨会话写入、进程崩溃/磁盘故障和实际导入补偿仍需专门验证；本主题不授权对真实配置进行保存、导入、回退或备份清理。
