---
knowledge_id: "delivery.plugin-catalog"
knowledge_type: "topic"
status: "current"
summary: "插件市场列表、详情投影、索引刷新与版本缓存；compact不代表按页读取源码数据，ready不证明全量刷新无错误。"
aliases: ["插件市场索引", "插件目录", "插件详情", "历史包分页", "hashPending", "plugin_index", "package_index", "MarketplaceCatalogService", "MarketplaceDataService", "refresh_plugin_index", "refresh_all_plugin_index", "plugin_catalog_signature", "get_plugin_latest_versions_cached", "view=compact", "view=update"]
code_paths: ["Web/Backend/marketplace_api_routes.py", "Web/Backend/services/marketplace_api.py", "Web/Backend/marketplace_services.py", "Web/Backend/services/plugin_index.py", "Web/Backend/plugin_marketplace.py", "Web/Backend/catalog_view_models.py", "Web/Backend/plugin_queries.py", "Web/Backend/services/app_latest_version_cache.py", "Web/Backend/services/storage_events.py", "Web/Backend/services/scheduler.py", "Web/Backend/db/repositories/index_state.py"]
test_paths: ["Web/Backend/test_app.py", "Web/Backend/test_plugin_index.py", "Web/Backend/test_app_latest_version_cache.py"]
related: ["delivery.backend", "delivery.artifact-delivery", "plugins.model", "plugins.getting-started"]
---

# 插件目录、详情投影与索引刷新

本页负责 Backend 的插件查询读模型，不是 WPF 插件装载器，也不负责发布包签名或设备运行。`marketplace_api_routes.py` 解析 HTTP 参数，`MarketplaceCatalogService` 组织响应，`MarketplaceDataService` 选择索引或磁盘回退。启动与数据库落点见 [Backend 组成](./README.md)。

## 文件是制品来源，索引是读取副本

`plugin_index` 保存插件元数据及 README/CHANGELOG 文本，`package_index` 保存当前/历史包版本、路径、大小、hash 等；`index_state` 记录刷新状态和目录签名。查询命中索引不检查每个文件现在是否仍存在，也不先等待 `index_state.status=ready`。因此列表、详情、版本探针和实际下载可能暂时不一致。

| 读取入口 | 当前来源与回退 |
| --- | --- |
| 列表 / 分类 | `get_request_plugin_catalog` 优先读取全部未删除的 `plugin_index` 行并合并当前下载总数；无行或索引读取失败时调用 `scan_plugin_summaries` |
| 单插件详情 | `get_plugin_info` 优先该插件的索引行及全部未删除的包行；缺行/读取失败时调用磁盘 `get_plugin_detail` |
| 版本探针 | `MarketplaceCatalogService.latest_versions` 走下节独立的进程内版本缓存，不等同于重新查询详情 |

磁盘回退可复用或写入 `cache_entry`、读取 manifest/README/CHANGELOG、枚举包；详情回退还可能读取包内元数据并计算文件 hash。它**不自动调用 `refresh_plugin_index` 或重建索引表**。旧 README 的“索引为空→扫描并写回索引”不是这条 GET 路径的真实契约。

`RequestContext.values` 的缓存仅复用同一个请求内的数据。持久缓存、索引、进程版本缓存各有更新路径，不能因一次查询返回新数据就推断所有读模型已同步。已经有索引时普通详情不为缺失的 hash 读取包：返回 `fileHash=null` / `hashPending=true`，需刷新路径补齐；不能把这一点扩大成所有 GET 分支都没有文件或缓存写入。

## 列表过滤与分页

`GET /api/plugins` 接受 `Keyword/keyword`、`Category/category`、`Author/author`、`SortBy/sort`、`SortOrder/sortOrder`、`Page/page`、`PageSize/pageSize`；同组优先读取前者。页号至少1，页大小1–100，默认1/20；非法整数或越界值返回400。排序允许 updated/name/downloads 与 asc/desc，兼容排序别名由 `normalize_catalog_sort` 维护，不是任意列名。

查询先拿完整目录，再在 Python 中过滤、排序和分页，不是数据库 LIMIT 查询。关键词在 name/id/description/author 中做不区分大小写的包含匹配，category 为不区分大小写的相等匹配，author 为包含匹配。超过末页会夹到最后一页；无结果时 page=1、totalPages=0。`categories` 来自未过滤目录，不只包含本页或当前关键词命中的分类。

## full、compact、update 是不同响应投影

`GET /api/plugins/<id>` 先执行完整 `get_plugin_info`，之后才按 `view` 构造响应。未指定或未知 view 沿 full 分支，并不因未知 view 返回400。

| view | 输出差异 |
| --- | --- |
| full | 插件级原始 README/CHANGELOG 与渲染 HTML，全部当前和历史版本 |
| compact | 省略原始 `readme/changelog`，保留完整 `readmeHtml/changelogHtml`；只对 `archivedVersions` 切页，`versions` 仍保留全部当前包 |
| update | 省略 README/HTML 等展示字段，保留插件级 `changelog` 和当前/历史版本元数据；历史版本不分页 |

compact 的 `archive_page`/`archivePage` 接受1–100000，`archive_page_size`/`archivePageSize` 接受5–100，默认1/20；只有 compact 校验这些参数，full/update 不使用它们。响应中的 `historicalPackageCount` 为全部历史数量，`archivedPage` 为夹到有效范围后的页号。响应缩小不代表后端只读取这一页，也不提供跨请求稳定快照；并行发布后下一页集合可能变化。

当前/历史包各自按 `package_sort_key` 的数字版本等键排序，不按 SQLite 版本字符串排序。详情版本行的 `requiresVersion` 使用当前插件级要求，不是读取每个历史包各自的要求；`downloadCount` 固定为0，不代表已统计该版本零次下载。总下载数另来自下载统计。版本行的 `changeLog/changeLogHtml` 在 full/compact 中保留为 null，避免为每个包重复整份日志。

## hash、刷新与完成边界

单插件 `refresh_plugin_index` 从磁盘构造摘要并准备包 hash，然后用一个数据库事务更新该插件及其包行。hash 可按文件的 mtime_ns/size 签名复用；这不是每次读取都重新计算的内容证明。之后再失效相关缓存和更新进程版本映射，这些动作不属于前述数据库事务。

全量 `refresh_all_plugin_index` 使用非阻塞的进程内全量锁；已有全量刷新时返回 `status=skipped, reason=refresh_in_progress`，不是等待已有任务完成，也不防止其它进程或单插件发布同时修改。

- 先记录扫描前 `plugin_catalog_signature`，再逐插件刷新，并将磁盘已消失的旧插件标记删除。各插件独立提交，全量没有单一快照或整体回滚。
- 逐插件 `refresh_plugin_index` 抛出的异常会进入 `errors`，结束时仍可返回 `status=ready`；`index_state.last_error` 只保留最近几条摘要。标删另有吞掉数据库异常后继续增加 `deleted_count` 的路径，所以即便errors为空或deleted_count大于0，也不证明消失插件已经标删。`ready`、计数、调度任务的成功文本都不能代替检查错误和目标条目。
- 结束时再次取签名，以 `changed_during_refresh` 标记扫描中变化，但保存的仍是扫描前签名，让下一次周期检查发现差异；本次不循环重试到全局一致。
- `plugin_summary_signature` 只检查元数据文件和当前/History 包的名字、大小、mtime_ns，不打开包做内容哈希。它可发现常规同名覆盖，但不能保证检测大小和时间戳都未变的内容变化。

缺失 hash 在刷新阶段计算；签名变化但新 hash 计算失败时会清除旧 hash，随后详情报告 pending，不能把旧 hash 当新文件的完整性证据。正常 GET pending 不自动补算，周期签名不变也不必触发刷新；需区分刷新未执行、hash失败和请求命中旧读模型。

### 谁会触发刷新

`startup_index_check` 在调度线程启动后执行；插件索引为空或活跃包缺 hash 时才做全量刷新，否则只报告 already populated，不在此分支重新比较插件目录签名。`plugin_index_check` 按持久 job 配置调度，默认5分钟；签名相同且状态 ready 时跳过，否则当前实现调用的是全量刷新，不是只刷新变更插件。

发布后的 storage event 可以只刷新对应插件。`MarketplacePackageService.publish` 的制品/元数据操作完成后，索引刷新是 best-effort，异常有被吞掉的分支。因此发布返回201不证明查询索引已经更新；不要用反复上传代替索引、缓存和实际文件的分层排查。手动刷新 CLI/API 会改写当前 Backend 数据库并读取制品，须先确认目标和授权，不能作为默认只读检查。

`POST /api/packages/publish` 的multipart入口使用 `PluginId`、`Version` 和文件字段 `package`；认证不能从公开GET接口推导，详见[HTTP认证](./authentication.md)。这是制品写入入口，不是查询探针。包下载的Range与服务端完成计数由[HTTP制品交付](./artifact-delivery.md)负责。

## 版本缓存不是每个请求都查磁盘

`get_plugin_latest_versions_cached` 按规范化 storage 根维护进程内整个插件版本 map，没有每次查询的 TTL/stat 检查。首次预热优先从索引取得非空版本；只有整份结果为空才扫描磁盘各插件的 `LATEST_RELEASE`，不会为索引缺失的单个插件逐项补读。map 已存在但某ID缺失时也直接省略，不能把 missing 当作文件必不存在。

`GET /api/plugins/<id>/latest-version` 返回纯文本或404；`POST /api/plugins/batch-version-check` 要求 PluginIds/pluginIds 为列表，逐项返回 ok/missing/invalid。安全ID去重后只批量取一次版本，但响应保留逐项结果。两者设置短期 public 缓存头不代表服务端 map 同时过期；文件手工变更需要刷新/预热等明确更新路径。

## 验证范围

`test_plugin_index.py` 覆盖索引读/回退、数字版本排序、hash pending及复用/变化、扫描中签名变化、非阻塞全量刷新和发布触发。`test_app.py` 覆盖compact响应与分页参数、列表过滤、批量版本的索引优先及进程缓存复用；`test_app_latest_version_cache.py` 只覆盖storage key的词法路径规范化，不是版本map行为全覆盖。现有测试未单独证明map已存在时缺失ID不会补读磁盘，该结论来自源码分支。这些是测试入口，不是本轮执行记录；它们也不证明生产索引完整、多进程并发安全、真实发布成功或所有请求都无磁盘I/O。
