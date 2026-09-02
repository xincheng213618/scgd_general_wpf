---
knowledge_id: "delivery.backend-public-data"
knowledge_type: "topic"
status: "current"
summary: "公共站点首页、发行归档、日志、工具、Android更新和目录浏览的读模型；compact路径不同，GET可写缓存或修复旧更新目录。"
aliases: ["公共站点读模型", "首页汇总", "发行归档分页", "changelog分页", "Android更新清单", "文件浏览", "api/site/home", "api/site/releases", "api/site/changelog", "api/site/browse", "get_compact_releases_from_index", "get_compact_home_releases_from_index", "build_index_page_context", "paginate_changelog_markdown", "build_android_update_manifest", "select_latest_android_release", "is_public_storage_path", "available_count"]
code_paths: ["Web/Backend/routes/pages.py", "Web/Backend/marketplace_services.py", "Web/Backend/page_contexts.py", "Web/Backend/services/artifact_index.py", "Web/Backend/services/storage_events.py", "Web/Backend/app_releases.py", "Web/Backend/app_changelog.py", "Web/Backend/storage_browser.py", "Web/Backend/services/public_storage.py", "Web/Backend/services/android_update.py", "Web/Backend/services/app_latest_version_cache.py", "Web/Backend/update_retention.py", "Web/Backend/services/spectrum_release.py", "Web/Backend/services/docs_site.py"]
test_paths: ["Web/Backend/test_app.py", "Web/Backend/test_artifact_index.py", "Web/Backend/test_page_contexts.py", "Web/Backend/test_app_releases.py"]
related: ["delivery.backend", "delivery.plugin-catalog", "delivery.artifact-delivery", "delivery.file-transfer", "delivery.backend-auth"]
---

# 公共站点数据、分页与文件可见性

`routes/pages.py` 提供JSON和下载入口，`MarketplaceDataService` 位于 `marketplace_services.py`，`page_contexts.py` 组织展示数据；React页面不是这些数据的事实源。本页负责非插件的公共查询和文件选择；[插件目录](./plugin-catalog.md)、[HTTP文件响应](./artifact-delivery.md)、[文件中转](./file-transfer.md)各有独立契约。[CVWindowsService服务包](./cvwindowsservice.md)的latest指针、独立releases缓存和按版本选包不属于通用tools列表。

查询存在不表示无副作用：回退可以写SQLite缓存，home/updates回退还可能修复旧更新目录。启动、配置和数据库隔离前提见[Backend组成](./README.md)，不要为验证文档随意启动服务或向实际存储发“只读探针”。

## 同名compact在不同入口含义不同

`view` 去空白并转小写，只有compact进入紧凑路径；未指定或其它值沿原完整路径，不统一返回400。公共页的 `_parse_int` 对空值取默认、非法整数返回400、越界整数夹到上下限；这与插件API的越界拒绝策略不同。

| 入口 | 取数与输出 |
| --- | --- |
| `/api/site/home?view=compact` | release索引ready时使用SQL限量发行读模型；否则回退旧home模型，最终只输出首页字段 |
| `/api/site/releases?view=compact` | ready时SQL分页；否则取旧发行上下文后在Python中过滤/分页 |
| `/api/site/changelog?view=compact` | 仅省略原文/分析/timeline，仍返回整份渲染HTML；提供page或page_size时才进入分页分支 |
| `/api/site/updates` | update索引或磁盘回退；本接口没有compact分页分支 |
| `/api/site/tools` | tool索引或目录回退，另读取Spectrum卡片；本接口没有compact分页分支 |

发行compact路径要求 `index_state` 的releases状态为ready，读取失败或不满足时返回None并走回退；完整 `get_releases_from_index` 则以活跃行是否存在为回退条件，不先等待ready。因此“compact正在回退”不等于“完全没有索引数据”。这些GET不承担重建发行索引的职责。

上传/发布写入后调用 `services/storage_events.py` 的 `on_storage_change` 按路径分流：Plugins定向刷新插件，Update刷新update_index，Tool刷新tool_index，其余路径走release_index；根 `LATEST_RELEASE` 特殊地只刷新进程版本缓存。刷新异常会打印后吞掉，这不是文件写入与索引/缓存的原子提交。周期签名检查的扫描深度见[内置任务](./jobs.md)。

## 发行归档：筛选总量、分页条数、当前包分开

Windows归档的 `major_minor`、`branch` 精确匹配，`kind` 大写化、`era` 小写化后筛选；这些筛选不限制当前包列表或Android归档。分页默认page=1/page_size=100、android_page=1/android_page_size=100；页号范围1–100000，页大小20–200。

索引快路径通过SQL汇总总量并按版本数字键、modified、relative_path倒序取LIMIT/OFFSET，再把本页Windows条目分组；不是先物化全部归档到Python。当前Windows和Android列表在这条路径各最多100项，不是所有字段都受归档page_size限制。回退使用旧完整模型再裁剪，不能拿快路径的内存/读取边界保证所有请求。

- `archive_visible_item_count` / `archive_visible_group_count` 是筛选后全部Windows归档数量；`archive_page_item_count` / `archive_page_group_count` 才是本页数量。
- 每组 `visible_count` 是全组筛选后总量，`page_item_count` 是本页切片长度，`visible_items` 只含本页条目；compact不重复一份组级items。
- Android归档独立页号、条数和总量，放在 `app_info.archived_android_releases`；不是Windows筛选的子集。
- 超出末页夹到最后有效页，无结果时有效页为1、total_pages为0。选项来自完整归档目录，而不是只来自本页。

计数、条目和过滤选项由多次查询取得，没有跨所有查询及发布操作的统一快照协议；不能承诺翻页期间总量不变。`latest_release` / `latest_android_release` 会优先current来源再比较版本，不是无条件选全归档中最大版本。`latest_version` 另来自进程版本缓存，两者可暂时不一致。

## 首页汇总不是整个存储的精确快照

首页独立取得release读模型、LATEST_RELEASE进程缓存、storage overview缓存、update/tool索引或回退，以及docs索引快照，再组装投影。request-local复用不把这些来源合成同一时刻。

compact发行快路径最多取6个当前Windows条目和4个最近Windows归档供构造首页；update索引命中时先读取完整活跃索引再取前8并构造update_summary，tool摘要也按预览项构造。更新预览的磁盘回退则先收集全部规范包元数据计算canonical_count/retained_count，仅把详细条目截前8；因此同名summary字段的统计范围还要区分索引与回退，不能统一当成整个存储总量。

完整首页先获取overview再按公开路径过滤并重算展示summary；overview缓存未命中会列存储根、统计各目录直接文件，并写缓存，不是递归文件总量。compact虽最后省略overview等展示字段，仍执行这段组装。docs摘要通过 `get_docs_index_snapshot(refresh_if_missing=False)` 获取Backend持有的文档快照，不保证当前源码知识目录已同步部署。

`build_index_page_context` 的更新预览回退、`build_updates_page_context` 的磁盘回退会进入 `repair_update_storage_layout`：可创建Update目录、移动旧 `ColorVision/Update` 文件、删除同名同大小旧副本并清理空旧目录。GET和自动HEAD都不能因此被当成零修改探针；具体修复和HTTP完成边界见[制品交付](./artifact-delivery.md#head-不计下载-但不承诺无本地修改)。

updates索引命中只返回已索引规范包，`other_update_files` / `other_update_items` 为空；磁盘回退还能列出其它文件，所以两种来源不是所有字段完全等价。tools在查tool_index之前就调用 `build_spectrum_tool_card` 读取独立Spectrum发布数据；tool索引命中也不能证明整个tools请求没有磁盘访问。

## changelog分页按二级标题，不是数据库分页

分页要求compact且请求参数中出现page或page_size（即使值为空）；默认1/20，page_size范围5–50，page范围1–100000。`build_paged_changelog_context` 先读取完整 `CHANGELOG.md`，再由 `paginate_changelog_markdown` 分段，只渲染选中段并缓存HTML；减少输出和渲染段数不等于只读文件的一段。

分段按逐行匹配 `##` 后空白，不解析Markdown语法树或验证版本标题；非版本二级标题也可能算entry，不能把total_entries直接解释成有效发布数。首页前导文本只随第一页返回；无二级标题时total_entries/total_pages为0但第一页仍可含完整文本。页号同样夹到有效范围。

无分页参数的compact返回整份 `changelog_html` 和latest_version；完整路径还含原文、分析和release_timeline。另一个 `/api/app/changelog` 返回纯文本，缺失或空内容为404；不要把两种接口的JSON/HTML/纯文本形状混用。

## Android更新清单：先选候选，再验证固定文件

`/api/android/update` 先调用发行读取（索引无行/失败可回退磁盘/缓存），从kind=APK且source=current中按数字版本、modified选最大，然后要求实际文件位于storage根，文件名严格为 `ColorVision-Android-<version>.apk`。不是任选子目录APK，也不把历史APK当当前更新。

无候选或选中的固定文件缺失/不符合路径要求，返回 `schemaVersion=1, available=false, release=null`；不会继续尝试次新候选。因而available=false不证明存储中没有任何可用旧APK，也不证明Android客户端配置错误。

成功清单包括version、filename、实际stat.size、sha256和固定downloadUrl。hash缓存键为 `android_update_sha256:v1`，签名含相对路径、大小、mtime_ns，TTL为30天；未命中会完整读取文件计算SHA-256并写缓存。它不是每次响应都重算的内容证明；同大小/时间戳覆盖、stat后并行替换、缓存失败和文件竞态没有快照或重试承诺。SHA-256也不等同于APK签名验证或客户端安装成功。

`/api/android/update/<version>/download` 只接受匹配版本的current APK候选，同版本取modified最大，再验证同一固定文件规则；不提供历史任意路径下载。manifest缓存60秒、APK响应max_age=300秒的HTTP策略与上述服务端hash缓存是不同层，详见[制品交付](./artifact-delivery.md#缓存、etag-与条件请求)。

## 浏览目录与实际下载的可见性

`/api/site/browse[/<path>]` 和 `/download/<path>` 共用 `is_public_storage_path`：History、Plugins、Spectrum、Tool、Update整个目录前缀公开；根目录还允许CHANGELOG.md、LATEST_RELEASE及匹配固定ColorVision发行文件名的制品。它按路径而非逐文件manifest、签名或“发布成功”状态判断；不要把敏感资料放入这些公开目录。

非公开普通路径需要共用策略的 `files:manage`，允许有该权限的普通Session，不限于角色名admin；无权限时通常隐藏为404。Transfer先走独立file:transfer认证和浏览器challenge，不因普通存储权限自动放行。规范化、实际目标resolve及storage边界检查另行执行，公开路径规则不是放弃路径限制。

目录请求只枚举一层、不递归：先列完整当前目录并排除点开头名字，再按公开可见性、q名称不区分大小写包含匹配、type过滤，目录优先/名称排序，最后offset/limit切片并为选中项构造详细记录。q最多100字符；type为all/directory/file；limit默认200、夹到1–1000，offset默认0、夹到0–100000。这是分页输出，不是只枚举limit个目录项。

`available_count` 是可见性过滤后、q/type前的条数，`total_count` 是q/type后分页前数量；summary只统计实际返回items及其中直接文件大小，不含目录递归大小。文件stat失败可能跳过条目，所以total_count不保证等于可成功读取的条目数。offset超过末尾返回空页而不夹回末页。

若目标本身是文件，接口直接返回is_file/name/subpath/download_url，不走目录参数校验，也不在这一步下载内容。隐藏名字的列表过滤不等于下载授权；列表或URL存在也不证明随后下载成功，文件可能变化或被清理。

## 验证与排查

`test_artifact_index.py` 有ready快路径不调用旧builder、5000条Android归档分页、组跨页总量、update/tool索引读取、browse过滤先于分页等用例；`test_page_contexts.py` 覆盖投影形状与仅为本页构造详细目录记录；`test_app.py` 覆盖分页参数夹取/非法整数、compact日志输出、Android清单与固定下载。`test_app_releases.py` 覆盖发行文件扫描和Android分组。

跨索引一致性、并行文件替换、失效Android最高候选不回退、公开GET修复旧目录等分支需要生产环境或故障注入验证。排查时先辨别快路径/回退、过滤总量/页内总量、文件存在/索引可见，再决定是否需要获准的刷新或文件维护。
