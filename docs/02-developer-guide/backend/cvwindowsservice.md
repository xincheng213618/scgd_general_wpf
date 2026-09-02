---
knowledge_id: "delivery.cvwindowsservice"
knowledge_type: "topic"
status: "current"
summary: "CVWindowsService 服务包的发布、LATEST_RELEASE、缓存与按版本选包；文件名通过不证明ZIP有效，发布不等于本机安装。"
aliases: ["CVWindowsService服务包发布", "服务包最新版本", "CVWS", "CVWS_PACKAGE_RE", "CVWS_RELEASES_CACHE_KEY", "choose_target_filename", "save_cvws_package", "update_cvws_latest_release", "build_cvws_page_context", "CvwsPublishPanel", "getCvwsContext", "publishCvwsPackage"]
code_paths: ["Web/Backend/routes/cvws_api.py", "Web/Backend/cvwindowsservice_publish.py", "Web/Backend/services/storage_events.py", "Web/Backend/storage_paths.py", "Web/Backend/routes/artifact_delivery.py", "Web/Backend/app_setup.py", "Web/Backend/config_loader.py", "Web/Frontend/src/pages/PublishPage.tsx", "Web/Frontend/src/services/site.ts", "Web/Frontend/src/types/site.ts", "Web/Frontend/src/utils/permissions.ts"]
test_paths: ["Web/Backend/test_cvwindowsservice_publish.py", "Web/Backend/test_cvws_web.py", "Web/Backend/test_contracts.py"]
related: ["delivery.backend", "delivery.backend-public-data", "delivery.backend-auth", "delivery.artifact-delivery", "plugins.windows-service"]
---

# CVWindowsService 服务包发布与选择

`routes/cvws_api.py` 与 `cvwindowsservice_publish.py` 管理 Backend 制品根目录下的 `Tool/CVWindowsService`，不是 WindowsServicePlugin 自身的 `.cvxp` 发布。服务端保存包、选择下载文件，不安装 Windows 服务、不执行 SQL，也不验证包能否运行；本机下载、安装、数据库迁移和旧工具入口见 [WindowsServicePlugin](../../04-api-reference/plugins/standard-plugins/windows-service.md)。

这里的发布 API 会公开文件并可能改变 `LATEST_RELEASE`；文档验证不授权上传、修改存储或安装服务。Backend 的配置/数据库不随 `--storage` 隔离，前提见 [Backend 组成](./README.md)。

## 四种读取不是同一个最新包

以下三个元数据 GET 及专门下载路由没有发布权限检查；`/admin/publish` 的页面权限不把这些读取变成私有 API。元数据读取可能写 SQLite 缓存，不是零写入探针。

| 入口 | 数据来源与选择规则 |
| --- | --- |
| `GET /api/tool/cvwindowsservice/releases` | 读取 `LATEST_RELEASE` 文本，另扫描包；返回 `latestVersion/packages/count`，没有分页 |
| `GET /api/tool/cvwindowsservice/latest-version` | 复用同一缓存/扫描函数，只投影 `version`；latest 文本为空返回404，不自动选择最高包 |
| `GET /api/tool/cvwindowsservice/context` | 绕过上述 releases 缓存，直接读指针并扫描，返回 `latest_version/packages/package_count/tool_dir_display` 等页面字段 |
| `GET /api/tool/cvwindowsservice/download/<version>` | 不读 latest 指针和 releases 缓存；重新枚举同版本包，选择数字 suffix 最大者 |

扫描仅枚举该目录的直接文件，名称须匹配 `CVWindowsService[x.y.z.w][-数字后缀].zip` 或历史 `.rar`，匹配不区分大小写；版本必须四段数字。条目包含文件名、名称中的版本与 suffix、实际 stat 大小/修改时间和通用 `/download/Tool/CVWindowsService/<filename>` URL，不包含内容 hash、签名、包内版本或结构验证。单文件 stat 抛出 `OSError` 会跳过该项，因此 count 是成功构造的条目数，不是所有目录文件数。

列表只按版本的四段数字倒序，同版本不再按 suffix 或修改时间排序，平局保持目录枚举顺序。`LATEST_RELEASE` 则是去首尾空白的 UTF-8 文本，没有版本格式、对应包存在性或单调递增检查；它可能指向旧版、缺失包或非版本文本。不能用列表第一项证明指针有效，也不能把 count 当成去重版本数。

专门下载先用通用 `is_safe_version` 检查数字/点形式，未通过返回400；这个检查允许非四段版本，但扫描只识别四段，因此形式通过仍可404。匹配版本是原始字符串相等，不把前导零归一化。无 suffix 按0，`-0522` 按522；`.rar` 与 `.zip` 一起比较，不优先 ZIP、较新 mtime 或“最后一次上传”，相同 suffix 取先枚举到的文件。目录或版本不存在为404。

包列表的 downloadUrl 指向精确文件名；专门按版本 URL 则可能在新增更大 suffix 后下载另一份内容，二者不能互换为不可变制品标识。专门路径直接使用枚举出的文件，没有调用通用下载的 `resolve_storage_file` 做 resolve 后的存储边界检查；`is_file()` 也不排除符号链接。不能把通用下载的路径防护推定为所有 CVWS 路径都具备，应保护实际制品目录的写入者与链接来源。

选定文件后走 [HTTP制品交付](./artifact-delivery.md)，支持其条件响应/Range 规则；本路由没有传 `on_completed`，构造 `ArtifactDownloadEvent` 不会自行增加插件完成下载统计，更不证明客户端收到或安装成功。

## releases 缓存不等于工具索引

`_get_cvwindowsservice_releases_payload` 使用键 `cvws_releases:v1`、TTL 180秒，签名为 latest 去空白文本加目录 `int(st_mtime)`。每次先读取 latest，再检查缓存；命中值只要求是字典，不再扫描包。签名没有包大小、包 mtime_ns 或内容 hash：同名原地覆盖不改变目录时间，或同秒目录变化，都可能暂时保留旧元数据。`context` 可比缓存列表新，按版本下载又直接读磁盘；这不是同一时刻的快照。

`/api/site/tools` 的 tool_index/回退及首页工具预览由 [公共站点主题](./public-data.md)负责，不消费这份 releases 缓存。发布显式失效 `cvws_releases:`、`home_tool_preview:`、`storage_overview:`，随后调用 `on_storage_change(..., "Tool/CVWindowsService")` 刷新 tool_index；刷新器异常可打印后吞掉，不能把发布201当成所有工具索引已同步的证明。

## 发布权限、字段与内容验证边界

`POST /api/tool/cvwindowsservice/publish` 要求 `release:publish`，允许具有实时权限的普通 Session，并沿共用策略接受管理员 Session、配置 Basic 或相应 Bearer；不是统一上传装饰器的 `plugin:publish`。凭据优先级、401浏览器挑战、403 `insufficient_scope` / `password_change_required` 和 CSRF 见 [HTTP认证](./authentication.md)。

请求是 multipart，文件字段 `package`；无文件/无文件名、扩展名不是 `.zip`、无法取得版本或版本格式不符返回400。全应用 `MAX_CONTENT_LENGTH` 为500 MiB，请求体含 multipart 开销；这不是每个 ZIP 解压后的大小上限。此链路只校验文件名/版本，不打开 ZIP，不检查 magic、CRC、解压路径、服务根目录、CommonDll、exe FileVersion 或签名。测试的成功样本也包含 `b"PKdata"`，不能称为完整服务包验收。

- `version` 先 strip；非空时使用四段数字校验。为空才从官方 ZIP 名推断，不猜任意 `service-1.2.3.4.zip` 中的数字；非官方 ZIP 可以显式提供版本后上传。
- `set_latest` 省略时默认 `"true"`；只 lower、不 strip，`1/true/yes/on` 为真，其余为假。因此省略不等于取消设置；`" true "` 也不等于真。
- 官方文件名与显式 `version` 没有一致性检查。若未冲突，上传 `CVWindowsService[1.0.0.0].zip` 配 `version=2.0.0.0` 可以保留1.0文件名、返回result.version=2.0并把指针写成2.0；后续扫描仍解析为1.0，按2.0下载可404。这是当前缺口，不是推荐的发布方式。

## 文件名冲突与部分成功

`choose_target_filename` 优先保留未占用的官方原名，即使同版本已有更高 suffix。仅当原名冲突或原名非官方，才按请求 version 枚举现有 ZIP/RAR：无同版本文件用标准名，有则取最大数字 suffix+1。例如已有 `-0522`，生成的是 `-523`，不是第二个日期序列。顺序调用会避开已发现的文件；名称检查与 `file_storage.save` 之间没有锁、排他创建或原子占位，因此不是并发绝不覆盖保证，也没有请求幂等键。

执行次序是创建目录/选择名 → 直接保存最终文件 → 可选直接覆盖 `LATEST_RELEASE` → 失效缓存/刷新工具索引 → 重读 latest/生成 releases 响应。保存 `OSError` 被转成500，但已经创建的目录或部分文件不自动清理；后续 latest/cache/响应读取失败也没有整链回滚。没有临时包原子替换、跨文件事务或跨发布请求锁，重试可能生成另一份后缀包。

成功201包含 `message/latestVersion/result/releases`，其中 `result.isLatest` 只是本次 `set_latest` 布尔值：未要求写指针但版本恰等于现指针仍可为false；并发发布后指针也可已改变。它不是“该文件是此版本下载胜者”“全站已刷新”或“所有客户端可用”的证明。这里也没有发行版本只能升不能降的门禁。

历史 `PUT /upload/<path>` 是另一个存储上传链（`marketplace_api_routes.py::legacy_upload`），已有测试使用它写入 `Tool/CVWindowsService`；不能把本节专门发布的字段、冲突重命名、指针更新和权限规则套到那个端点。

## 网页调用与本机安装分层

React `PublishPage.tsx::CvwsPublishPanel` 在 `getAdminPublishCapabilities().publishReleases` 为真时展示。它加载 `getCvwsContext`，默认勾选“设为最新版本”，提交文件和两个表单字段；表格下载按钮使用条目的精确 downloadUrl。表格分页只是客户端显示，不使 context 成为服务端分页 API。

提交成功后页面提示“服务包已发布”、清空所选文件/版本并重新加载 context；`load` 吞掉错误，故刷新失败可留下旧列表而不把提交判为失败。上传进度、成功提示和页面的“当前版本”分别来自不同阶段，都不验证 Windows 端安装。`FilesPage` 的路径输入只提供 `Tool/CVWindowsService` 示例，走通用 browse，不能当作专门发布消费者。

## 验证与诊断

`test_cvwindowsservice_publish.py` 覆盖名称/版本正则、推断、顺序冲突后缀、直接保存、latest写入和context字段；`test_cvws_web.py` 覆盖未认证/浏览器挑战、Session上传、显式set_latest真假、基础列表/latest/下载及legacy PUT。`test_contracts.py` 另覆盖不存在版本404、撤销发布Session及普通角色缺少release:publish。测试名含“without_set_latest”的用例实际传的是 `"false"`，不能当作省略字段的覆盖证据。

测试里的 `import app` 发生在临时路径替换之前，不能因为 setUp 有临时目录就推定整个导入过程隔离。并发占名/文件替换、官方名与手填版本冲突、缓存同秒失效、RAR平局、symlink边界、发布后失败回滚和真实服务安装均没有由上述用例证明。

排查先分清：latest文本、扫描列表、按版本选中的文件、工具索引、客户端缓存、实际安装状态。获得目标存储与发布授权后才考虑重传/改指针；重新发布不是无副作用的验证方法。
