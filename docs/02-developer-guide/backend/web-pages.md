---
knowledge_id: "delivery.web-pages"
knowledge_type: "topic"
status: "current"
summary: "React 页面和 VitePress 文档的托管、开发代理、缓存、Brotli/gzip 与 Range 协商、旧分块恢复及文档索引；后台索引刷新不构建网页，也不证明已部署内容最新。"
aliases: ["Web 页面托管", "前端白屏", "静态文件压缩", "Brotli", "SPA", "Vite 开发代理", "vite:preloadError", "installChunkRecovery", "colorvision-web-chunk-reload-at", "FrontendSpaContext", "_send_static_file", "Documentation site has not been built", "Web frontend has not been built", "文档中心", "文档索引", "docs/status", "index/docs/refresh", "resolve_docs_site_file", "docs_index_signature", "get_docs_index_snapshot"]
code_paths: ["Web/Backend/routes/frontend_spa.py", "Web/Backend/routes/docs_site.py", "Web/Backend/services/docs_site.py", "Web/Backend/routes/admin_api.py", "Web/Backend/app_setup.py", "Web/Backend/page_contexts.py", "Web/Backend/services/http_security.py", "Web/Frontend/vite.config.ts", "Web/Frontend/package.json", "Web/Frontend/src/main.tsx", "Web/Frontend/src/utils/chunkRecovery.ts", "Web/Frontend/scripts/precompress-static.mjs"]
test_paths: ["Web/Backend/test_frontend_spa.py", "Web/Backend/test_docs_site.py", "Web/Backend/test_contracts.py", "Web/Backend/test_http_security.py", "Web/Frontend/tests/viteConfig.test.ts"]
related: ["delivery.backend", "delivery.web-deployment", "platform.web-architecture", "delivery.artifact-delivery", "delivery.backend-public-data", "delivery.backend-auth", "governance.maintenance"]
---

# Web 页面与文档托管

Flask 分别托管 React 应用和 VitePress 文档。两者使用不同的构建目录、URL 解析和响应策略；后端文档索引又是单独的缓存。排查页面缺失、部署后白屏或文档内容未更新时，先确认请求属于哪条链。

| 内容 | 构建/数据位置 | 服务入口 |
| --- | --- | --- |
| React 门户、账户和管理界面 | `Web/Frontend/dist` | `routes/frontend_spa.py` 返回 React 入口及资源 |
| VitePress 文档网页 | `docs/.vitepress/dist` | `routes/docs_site.py`，根路径 `/scgd_general_wpf/` |
| Backend 文档目录摘要 | 扫描 `docs/**/*.md`，写入 Backend 缓存 | `services/docs_site.py`；用于状态和首页摘要 |
| 知识目录、网页搜索索引 | 原始 Markdown 元数据及网站构建产物 | [知识维护规范](../../knowledge/maintenance.md)，不是 Backend 文档缓存的副本 |

本地启动和 NAS 部署的前提、命令及产物更新顺序见 [Web 本地启动与 NAS 部署](../deployment/web.md)。本页描述服务行为，不要求启动生产后端或部署来验证文档。

## 前端开发与构建

在 `Web/Frontend/` 运行 `npm install` 安装依赖、`npm run dev` 启动 Vite 开发服务。Backend 需另外按[配置与启动说明](./README.md#启动与配置)准备并启动；前端开发服务本身不创建 Backend 账号、数据库或 API。

`vite.config.ts` 的 base 为 `/`，只将 `/api` 代理到 `http://127.0.0.1:9998`，`changeOrigin=false` 保留浏览器请求的 origin/host 关系。`/login`、`/logout` 没有开发代理配置；其它下载和文档路径也不会因为 API 代理存在而自动转发。Backend 端口变化时须同步核对代理目标，认证及 CSRF 规则见 [HTTP 认证](./authentication.md)。

| 前端命令 | 当前职责 |
| --- | --- |
| `npm run lint` | ESLint 检查 |
| `npm run test` | Node 类型擦除模式运行 package.json 中显式列出的测试；需要支持该模式的 Node |
| `npm run build` | TypeScript 项目编译、Vite 带 manifest 构建、静态预压缩、管理首页包体预算检查 |
| `npm run preview` | Vite 预览入口；不能替代 Flask 路由、鉴权和压缩协商验收 |

版本和运行时要求以 package.json、lockfile 及已安装工具的声明为准。构建成功不表示 lint、测试或真实浏览器流程都已运行；具体包体预算见 [Web 架构](../../03-architecture/components/web.md#性能预算与现有检查)。

## React 入口和静态资源

Flask 只为明确注册的应用路径返回 `index.html`，不会将所有 404 改成 React 首页：

| 路径 | 行为 |
| --- | --- |
| `/`、`/plugins` 及其子路径、`/releases`、`/changelog`、`/updates`、`/tools` | 公共 React 入口 |
| `/transfer`、`/transfer/share/<路径>`、`/account`、`/browse` 及其子路径 | 返回 React 入口；后续 API 仍按各自权限执行 |
| `/admin`、`/admin/` 及其子路径 | 先执行注入的管理认证检查，未通过则 302 到 `/login?next=...`，next 包含当前内部路径及查询串 |
| `/assets/<路径>` | 构建资源；成功的 200/206/304 使用 `public, max-age=31536000, immutable` |
| `/brand/<路径>`、`/media/<路径>`、`/favicon.svg`、`/favicon.ico` | 普通静态资源，max_age 为 3600 秒；ico 映射到 `brand/colorvision.ico` |

管理入口能返回 HTML 不代表所有管理 API 都已授权。Flask 返回某个子路径的 React 入口，也不表示 React Router 一定定义了该页面。

普通运行中 `dist` 不存在时返回 503 和 `Web frontend has not been built...`；`TESTING` 模式在这个条件下可返回合成的空 root HTML。已有目录中缺少选中的文件走文件缺失处理，不能一概按 503 排查。React 入口 HTML 使用 `no-cache, must-revalidate`，让下次导航重新验证引用的分块名称。

找不到 `/assets/...` 资源时保持 404，不返回 HTML 冒充 JavaScript。`immutable` 是该资源路由的缓存策略，不是服务端逐个验证文件名或内容确实不可变。

## Brotli、gzip 和 Range

前端构建为 HTML/CSS/JS/JSON/SVG 生成更小的 `.br`、`.gz` 旁文件，并解压比对；没有压缩收益时不保留对应变体。运行时 `_send_static_file` 从已存在的旁文件选择响应，不现场重新压缩或解压验证。

客户端请求原始 URL，例如 `/assets/app-<hash>.js`，通过 `Accept-Encoding` 协商：

- 根据 q 值和可用文件选择 br、gzip 或 identity；压缩候选顺序为 br、gzip。不支持或缺少某种变体时，不能仅凭请求头推断已压缩。
- 未提供请求头时允许 identity。显式 `identity;q=0` 拒绝原表示；没有显式 identity 且 `*;q=0` 也拒绝原表示。没有任何可接受表示时返回空 406。
- 存在压缩旁文件的响应，以及协商失败的 406，添加 `Vary: Accept-Encoding`。压缩响应设置 `Content-Encoding`，文件类型按原始文件名传给 Flask 判断。
- 直接请求资源的 `.br` 或 `.gz` 后缀返回 404；这些旁文件不是公开资源 URL。

存在两种旁文件时的典型结果：

| 请求 | 结果 |
| --- | --- |
| `Accept-Encoding: br, gzip` | 选择 br |
| `Accept-Encoding: br;q=0, gzip;q=1` | 选择 gzip |
| `Accept-Encoding: gzip;q=0.5, identity;q=1` | 原始文件 |
| `Accept-Encoding: *;q=0` | 406 |
| `Range: bytes=0-99`，仍接受 identity | Range 针对原始文件字节，忽略可选的压缩表示 |
| `Range: bytes=0-99` 且 `Accept-Encoding: gzip, identity;q=0` | 有 gzip 变体时，Range 针对压缩文件字节；Content-Range 总长度也是压缩长度 |

HEAD 选择与 GET 相同的表示和长度，但无响应体。ETag、Last-Modified 和条件请求基于选中的文件；不同表示的验证器不能互换。If-Range 不匹配可能得到完整 200，不能总按 206 分片解析。

此处静态文件协商与 [HTTP 制品交付](./artifact-delivery.md#gzip-只处理合格的缓冲-json)中的缓冲 JSON gzip 钩子是两条实现。文档网页路由也没有复用这里的预压缩选择器。

## 部署后旧页面引用失效分块

`main.tsx` 安装 `installChunkRecovery`，监听 `vite:preloadError`。事件触发后阻止默认错误传播，以 sessionStorage 的 `colorvision-web-chunk-reload-at` 记录重载时间，并调用 `window.location.reload()`；距上次记录不足 60 秒时不再重载。

这是一项按时间限制的自动重试，不是每次部署仅重载一次，也不保证新的请求成功。事件没有按 404、网络故障或版本不匹配分类；冷却期内仍阻止事件默认处理，sessionStorage 访问失败没有专门降级。排障时同时核对旧分块请求的状态、最新 index.html、浏览器存储和控制台，不能以“已自动刷新”作为故障已恢复的判断。

## VitePress 路径与缺页处理

`/docs` 和 `/docs/` 以 302 转到 `/scgd_general_wpf/`；`/docs/<路径>` 转到相同文档根下的路径，`/scgd_general_wpf` 补末尾斜杠。这些重定向只拼接路径，不显式保留查询参数。

解析器在 `docs/.vitepress/dist` 中按以下规则找文件：根入口选 `index.html`；目录先找 `index.html`、再找 `README.html`；再检查原目标，目标没有后缀时补 `.html`。候选文件须经解析后位于 dist 内；路径不合法或文件缺失时尝试 `404.html` 并返回 404。

dist 目录不存在时返回 503。找不到页面且没有 `404.html` 时返回 404，但当前响应正文仍使用 `Documentation site has not been built...`，不能仅凭这句话判断整个站点未构建。常规候选路径的目录约束不应推广为所有部署文件或备用 404 文件都经过同等检查。

HTML 响应设置 no-cache，其它资源设置 public 和 3600 秒缓存；这里直接使用 `send_from_directory`，不选择 React 的 `.br/.gz` 变体，也不应用 React 的 immutable 策略。路由随后用解析状态覆盖 response.status_code，因此不能把 React 静态文件的条件请求测试直接当作文档路由的 304/Range 证明。文档路径的 CSP 允许 VitePress 所需 inline script，具体响应头见 [HTTP 响应策略](./artifact-delivery.md#浏览器响应头是基线-不是内容安全认证)。

## 文档索引和状态查询

Backend 文档索引从 Markdown 取得标题、首段摘要、路径、分类、语言、大小和修改时间，并按语言、分类、路径排序，另外保留最近 8 项。它不是 Markdown 全文搜索，也不读取知识 frontmatter 的 status/search 来决定可见性；扫描只排除 `.vitepress` 和 `node_modules`，因而数量可能包含说明文件、历史主题或兼容入口，不等于当前知识主题数或构建 HTML 数。

索引签名对相对路径、文件大小和纳秒修改时间做 SHA-256，不是文档内容哈希。缓存键为 `docs_index:v2`，TTL 30 天；`get_docs_index` 每次先计算源码签名，未命中时可重建缓存。`get_docs_index_snapshot` 默认只读既有缓存，不校验源码签名；首页使用这条快照路径，详见[公共站点数据](./public-data.md)。

| 接口 | 权限 | 当前行为 |
| --- | --- | --- |
| `GET /api/admin/docs/status` | `cache:read` | 读取源码/HTML 数量、构建与搜索文件状态；会调用可能刷新缓存的索引查询 |
| `POST /api/admin/index/docs/refresh` | `cache:refresh` | 重建 Backend 索引和状态，尝试写审计；不运行 VitePress、不部署网页 |

认证和 Session 写入的 CSRF 前提见 [HTTP 认证](./authentication.md)。刷新 helper 的常规异常可转换为 `status=error`，路由仍以 JSON 返回而未设置非 200 状态，须检查业务字段；索引状态写入本身的异常也可能直接传播。`POST /api/admin/index/refresh-all` 包含 docs，而 Backend CLI 的 `--refresh-all-indexes` 不包含 docs。

状态的 `healthStatus` 依次检查：源码目录缺失为 error；未生成首页、后台索引为空、搜索索引文件缺失分别为 warning；否则为 ok。manifest 是否存在仅作为字段返回，不是 ok 的必要条件。文件存在、索引有内容和时间字段不证明源码与网页同版本，也不证明搜索 JSON 内容有效。

看到“索引已更新、网页仍旧”时，应核对并重建实际服务目录里的文档站点。`Run-Web` 会复用已有文档首页，NAS 部署脚本不构建文档；需要按[知识维护规范](../../knowledge/maintenance.md)生成并验证产物，不能反复刷新 Backend 索引代替网页构建。

## 对照测试与验证范围

`test_frontend_spa.py` 覆盖 React 路由、缺资源 404、HTML 重验证、Brotli/gzip/identity、406、HEAD、ETag 及 Range 表示；`viteConfig.test.ts` 核对 API 代理保留 origin，并确认 login/logout 不代理。`test_docs_site.py` 检查摘要读取，`test_contracts.py` 用临时站点覆盖文档重定向、clean URL、状态字段和索引刷新。

这些用例不证明真实浏览器旧分块重载、反向代理缓存、文档条件请求或部署目录权限已验收。源码引用和文档构建也不能替代这些运行验证。
