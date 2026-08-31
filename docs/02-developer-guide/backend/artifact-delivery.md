---
knowledge_id: "delivery.artifact-delivery"
knowledge_type: "topic"
status: "current"
summary: "Backend HTTP制品交付的Range、完成事件、下载计数、Cache-Control/ETag、HEAD副作用与JSON gzip边界；服务端迭代完成不证明客户端落盘。"
aliases: ["HTTP制品下载", "下载完成计数", "Range", "Content-Range", "HEAD下载", "ETag", "Cache-Control", "no-store", "gzip", "Accept-Encoding", "ArtifactDeliverySpec", "ArtifactDeliveryService", "ArtifactDownloadEvent", "deliver_artifact", "_CompletionTrackingIterator", "_completed_representation_bytes", "record_download", "repair_update_storage_layout", "register_response_compression"]
code_paths: ["Web/Backend/services/artifact_delivery.py", "Web/Backend/routes/artifact_delivery.py", "Web/Backend/marketplace_api_routes.py", "Web/Backend/services/marketplace_api.py", "Web/Backend/marketplace_services.py", "Web/Backend/download_stats.py", "Web/Backend/routes/pages.py", "Web/Backend/update_retention.py", "Web/Backend/services/http_security.py", "Web/Backend/services/http_compression.py", "Web/Backend/app.py", "Web/Backend/app_setup.py", "Web/Backend/services/access_analytics.py"]
test_paths: ["Web/Backend/test_artifact_delivery.py", "Web/Backend/test_http_security.py", "Web/Backend/test_http_compression.py", "Web/Backend/test_app.py"]
related: ["delivery.backend", "delivery.file-transfer", "delivery.plugin-catalog"]
---

# HTTP 制品交付、完成计数与响应策略

本页负责已经选定文件后的 HTTP 响应及完成事件，不负责证明客户端收齐、落盘、安装或内容可信。Backend 启动与存储前提见 [Backend 组成](./README.md)，上传、覆盖和公开分享见[文件中转](./file-transfer.md)，插件读模型及包 hash 见[插件目录](./plugin-catalog.md)。认证与 CSRF 由[HTTP认证](./authentication.md)主题负责；交付服务本身不授予访问权限。

## 文件响应与完成事件是两个步骤

`routes/artifact_delivery.py::deliver_artifact` 将路径、下载名称、MIME、`etag`、`max_age` 与 `ArtifactDownloadEvent` 组成 `ArtifactDeliverySpec`，通过 `ArtifactDeliveryService.deliver` 调用 Flask `send_file(conditional=True, ...)`。文件及条件请求、Range 响应由框架处理；服务补充默认 `Accept-Ranges: bytes` 并设置 `X-Content-Type-Options: nosniff`。调用路由仍负责参数、路径、文件存在性和授权检查，不能绕过路由直接调用服务来获得这些保证。

只有调用方提供 `on_completed`，且响应满足以下条件，服务才包装响应迭代器：

| 响应 | 是否符合完成事件的候选条件 |
| --- | --- |
| GET 200 | 有可解析且非负的 `Content-Length` |
| GET 206 | `Content-Range` 严格匹配 `bytes 0-N/Total`，且 `N+1 == Total == Content-Length`，即单次响应覆盖整个表示 |
| HEAD、304、其它状态、未知长度 | 不符合 |
| 只返回部分内容的 206 | 不符合；不同请求的分片不会合并计算 |

候选响应不是立即计数。`_CompletionTrackingIterator` 累加已产出的字节数，直到源迭代器抛出 `StopIteration`，且字节数恰等于预期长度，才触发一次回调。提前 `close()` 只关闭源，不补发完成事件；迭代异常、长度不符也不算完成。即便最后一块已经产出，未继续迭代到结束就关闭，也不会触发回调。

因此，200 响应头或 `bytes=0-` 请求本身不代表已计数；完整 206 可以计数，分段续传即使客户端最终拼齐也不累计成一次完成。这里观察的是**服务端响应迭代结束**，不是网络确认、客户端下载成功或磁盘写入确认；不能据此推导真实终端成功率。

## 插件下载数的真实 owner

`marketplace_api_routes.py::api_download_package` 的 `/api/packages/<plugin_id>/<version>` 调用显式传入完成回调，经 `PackageService.record_download`、数据服务转到 `download_stats.py::record_download`，向 `download_log` 插入一行。重复完整下载可产生多行，没有按客户端或同一制品请求去重的保证。

通用 `/download/<path:relative_path>` 等调用只使用交付响应、没有该插件完成回调；通过通用路径下载同一插件文件，不会自动更新插件下载数。下载统计与[插件目录](./plugin-catalog.md)中的总数读取相连，但不是全部文件响应的统一计数器。

完成回调发生异常时，迭代器记录提示但不重试；`download_stats.record_download` 自身还会吞掉数据库异常。因此内容已经输出不保证统计成功持久化，统计也不与文件发送形成事务。

`app_setup.py` 的请求日志/访问记录与 `services/access_analytics.py` 是另一条统计链：在响应钩子阶段使用声明的响应长度等元数据，不等待这里的制品完成迭代。长期访问报表不能直接当作插件完成下载记录使用。

## 缓存、ETag 与条件请求

`ArtifactDeliverySpec` 默认 `etag=True`、`max_age=None`，适配器原样交给 `send_file`。ETag 是框架的文件缓存验证器，当前适配器没有用插件 `fileHash` 或 SHA-256 内容哈希生成它；调用方也可传入自定义字符串。不能把 ETag 当成包完整性或可信来源证明。条件请求可能返回304，也可能返回200；后者仍按上面的完整迭代规则处理。

缓存策略必须看具体响应，不能由“API”或“需要登录”推断：

- `register_response_security` 只对 `/api/admin/`、`/api/auth/`、`/api/ops/`、`/api/transfer/` 前缀，在尚无 `Cache-Control` 时补 `no-store`。它使用 `setdefault`，不会覆盖路由或 `send_file` 已设置的策略。
- `routes/pages.py` 的 Android 更新 manifest 显式设置 `public, max-age=60`，对应 APK 下载给交付适配器传入 `max_age=300`。其它交付调用可以采用不同策略。
- 是否保存、何时重新验证与内容真实性是不同问题；排查缓存时同时核对路由、`Cache-Control`、ETag 和实际200/304，不能只看某个通用响应钩子。

## 浏览器响应头是基线，不是内容安全认证

`http_security.py` 对响应补充CSP、nosniff、SAMEORIGIN、same-origin Referrer-Policy，以及禁用camera/microphone/geolocation/payment/usb的Permissions-Policy；均使用 `setdefault`，保留已存在的路由策略。默认CSP限制script/connect等为同源，但style允许inline、img允许data与https，因此不能概括成所有资源都严格同源。

文档路径 `/scgd_general_wpf` 及其子路径另允许inline script，以兼容VitePress生成的启动脚本；其它路径没有这个脚本例外。该选择按请求路径，不是按文件是否实际来自VitePress判断。响应头约束不等于内容已经安全审计，也不能替代路由权限或包签名验证。

## HEAD 不计下载，但不承诺无本地修改

HEAD 不触发本页的下载完成事件，客户端也不接收响应体；它仍可能执行 GET 路由中的准备逻辑。

`routes/pages.py::api_app_incremental_download` 在定位目标包之前无条件调用 `repair_update_storage_layout`。因此对 `/api/app/updates/<version>/download` 发 HEAD，甚至最终目标不存在而返回404，也可能先修复旧目录：从 `storage/ColorVision/Update` 移到 `storage/Update`、创建目标目录，并清理空的旧目录。若目标同名文件已存在且大小相同，修复代码直接删除旧副本，未比较内容 hash；大小不同则为迁移文件添加时间戳后缀。

这是已有路由副作用，不是建议用 HEAD 触发迁移，更不是“只探测不会改文件”的保证。文件中转的到期处理另见[文件中转](./file-transfer.md)，不要将不同路由的 HEAD 边界混为一个全局承诺。

## gzip 只处理合格的缓冲 JSON

`services/http_compression.py::register_response_compression` 在 `app.py` 注册，默认最小响应体为1024字节。它只考虑 GET/HEAD、200、`application/json` 或 `application/*+json`；以下任一条件都会跳过：

- `direct_passthrough` 或流式响应。
- 已有 `Content-Encoding`、`Content-Range` 或 ETag。
- `Cache-Control` 包含 `no-transform`，或 JSON 小于阈值。

达到阈值的候选响应先添加 `Vary: Accept-Encoding`，再判断客户端是否接受 gzip；显式 `gzip;q=0` 不压缩，未指定 gzip 时可由可接受的 `*` 启用。使用 `gzip.compress(compresslevel=6, mtime=0)`，只有压缩后更小时才设置 `Content-Encoding: gzip` 与压缩后的 `Content-Length`，否则保留原表示。此钩子没有 Brotli/deflate 分支。

所以请求 `Accept-Encoding: gzip` 不会让 APK、普通文件流或带 ETag 的 JSON 必然压缩。HEAD 也进入这一钩子，可能读取已缓冲 JSON 并执行压缩来生成表示头；没有网络响应体不代表没有这部分计算。

## 失败定位与验证边界

- “200 但计数没增加”：先确认走插件包专用路由且有回调，再查是否完整迭代、长度/Range 是否符合、持久化是否失败；不要把状态码当作回调证据。
- “HEAD 改了目录”：检查调用路由进入交付服务前的逻辑，增量包重点看 `repair_update_storage_layout`。交付服务不计数不等于整个请求无副作用。
- “no-store/gzip 与预期不同”：核对具体路径和已有响应头；gzip 再看类型、缓冲/流式、阈值、协商与压缩收益，不从全局配置推断单个响应。

现有 `test_artifact_delivery.py` 覆盖完整 GET、HEAD、304、部分/完整 Range 和提前关闭迭代器；`test_app.py::test_api_download_package_updates_stats_and_plugin_totals` 覆盖专用包路由的 HEAD/部分 Range 不计数及完整 GET 计数。`test_http_security.py` 覆盖补充 `no-store` 而不覆盖显式策略；`test_http_compression.py` 覆盖大 JSON、gzip质量值、通配符与小 JSON。

`test_app.py` 的增量更新/旧下载路径用例覆盖 GET 触发目录修复，不等于已验证 HEAD 修复的部署表现。上述源码分支与测试也不证明真实网络断连、反向代理、WSGI服务器关闭时机、客户端下载落盘或回调失败后的统计补偿。本次文档维护未运行这些产品测试。
