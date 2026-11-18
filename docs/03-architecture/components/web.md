---
knowledge_id: "platform.web-architecture"
knowledge_type: "topic"
status: "current"
summary: "Web 的组成根、HTTP/服务/持久化边界、现有接口和架构检查；区分已实现约束、性能预算与后续演进目标。"
aliases: ["Web 架构","Web architecture","架构边界","ArchitectureBoundaryTests","MarketplaceContext","MarketplaceApiRouteContext","IndexStateRepository","JobRepository","OperationsAdminQuery","OperationsSupportStore","check-dashboard-bundle","前端包体预算","ArtifactStore","OpenAPI"]
code_paths: ["Web/Backend/app.py","Web/Backend/app_setup.py","Web/Backend/context.py","Web/Backend/marketplace_api_routes.py","Web/Backend/ports/","Web/Backend/db/repositories/","Web/Backend/routes/artifact_delivery.py","Web/Backend/services/artifact_delivery.py","Web/Backend/routes/auth_adapters.py","Web/Backend/services/auth_policy.py","Web/Backend/services/storage_events.py","Web/Backend/services/plugin_index.py","Web/Backend/services/artifact_index.py","Web/Backend/routes/frontend_spa.py","Web/Frontend/src/App.tsx","Web/Frontend/package.json","Web/Frontend/scripts/check-dashboard-bundle.mjs","Web/Frontend/scripts/precompress-static.mjs"]
related: ["delivery.backend","delivery.artifact-delivery","delivery.backend-auth","delivery.backend-observability","delivery.backend-jobs","delivery.plugin-catalog","delivery.backend-public-data"]
---

# Web 架构与演进边界

ColorVision Web 由 React 前端与 Flask 后端组成，提供公共门户、插件/更新分发和管理页面。前端构建产物由后端托管；制品文件系统是发布内容来源，SQLite 负责索引、缓存、账号、审计和任务等数据。

本页说明跨模块责任及演进约束。运行配置和命令见[后端组成与 CLI](../../02-developer-guide/backend/README.md)，具体接口以各功能主题为准。

## 当前模块责任

| 责任 | 当前实现 |
| --- | --- |
| 组成根 | `app_setup.py:create_app_and_context` 创建 Flask、服务、数据库和依赖；`register_all_blueprints` 装配 HTTP 路由 |
| 可执行/兼容入口 | `app.py` 调用组成根，保留旧全局变量和无状态 helper 转发，主入口处理 CLI 和调度线程启动 |
| 依赖传递 | `MarketplaceContext` 持有运行配置访问器、数据库、缓存和服务；市场路由使用较小的 `MarketplaceApiRouteContext` |
| HTTP 适配 | `routes/` 处理请求/响应与认证声明；市场核心路由仍位于根目录 `marketplace_api_routes.py` |
| 用例与策略 | `services/` 和根目录的 `marketplace_services.py` 等模块组织业务，已分离的策略接收显式请求身份与依赖 |
| 持久化 | `ports/` 声明接口，`db/repositories/` 提供对应 SQLite 实现；schema 演进归 `db/schema_version.py` |
| 页面与客户端 | `Frontend/src/App.tsx` 组织路由，`pages/`、`layouts/`、`components/`、`services/`、`types/`、`utils/` 承载当前前端 |
| 静态托管 | `routes/frontend_spa.py` 服务 `Frontend/dist` 和已知 SPA 路径，保留 API/文件路由的独立响应 |

组成根可以装配各层。新增 HTTP handler 应把业务操作交给服务，并将 SQL 放进所属 repository；已分离的服务通过参数接收身份、时间和路径，不读取 Flask `request`、`session` 或 `g`。不要因新增目录或接口就把所有旧模块宣称为已迁移。

`app.py` 的兼容访问器仍允许旧调用方改变运行配置、存储或数据库目标；组成根不反向导入 `app`。只有测试、脚本和外部 WSGI 消费方都完成迁移后，才可移除旧全局变量或导入转发。具体启动副作用见[后端组成](../../02-developer-guide/backend/README.md#启动与配置)。

## 已有持久化接口

| 接口 | 所属边界 |
| --- | --- |
| `IndexStateRepository` | 索引状态、签名、耗时与错误的读写；不是所有制品查询 SQL 的统一接口 |
| `JobRepository` | 内置任务定义、运行登记、单飞、恢复与历史保留 |
| `OperationsAdminQuery` | 运维管理页的有界只读投影 |
| `OperationsSupportStore` | 支持会话状态与事件的持久化 |

各接口定义在 `Web/Backend/ports/`，实现在 `db/repositories/`。当前 `services/` 仍主要是平铺模块，前端也仍使用 `pages/` 等目录；`features/marketplace` 或 `services/releases/` 这样的功能目录属于后续组织方向。

### 直接 SQL 的过渡范围

直接调用 `execute`、`executemany` 或 `executescript` 的过渡范围限于现有 handler：

- `routes/admin_api.py`：`stats_overview`。
- `routes/operations_relay.py`：`heartbeat`、`poll_tasks`、`create_task`、`task_receipt`、`list_hosts`、`list_receipts`、`list_support_events`。

`create_api_key` 不在例外范围内。新增功能应通过服务或 repository 访问数据库，迁移后的 handler 随之退出过渡范围。

## 跨模块契约

| 边界 | 维护方式 |
| --- | --- |
| HTTP 制品交付 | endpoint 负责认证和安全路径解析，交给 `ArtifactDeliveryService` 与 Flask adapter 处理表示、Range 和完成回调，详见[交付契约](../../02-developer-guide/backend/artifact-delivery.md) |
| 身份与权限 | `AuthPolicy` 处理凭据和范围判断，Flask adapter 处理请求上下文与响应，详见[HTTP 认证](../../02-developer-guide/backend/authentication.md) |
| 上传后的索引 | `services/storage_events.py` 按路径分派刷新；当前仍有中央条件分支，文件、索引和缓存并非一次原子提交，详见[公共读模型](../../02-developer-guide/backend/public-data.md) |
| 调度与刷新互斥 | 任务运行登记由 SQLite repository 提供；插件全量及制品作用域刷新另有进程内锁，不能把任务单飞推定为多 WSGI worker 下全部刷新路径已互斥，详见[任务](../../02-developer-guide/backend/jobs.md)与[插件索引](../../02-developer-guide/backend/plugin-catalog.md) |
| 观测数据 | HTTP、SPA 导航、下载完成和审计各有口径与保留范围；身份 HMAC、代理地址和查询字段限制见[访问及性能观测](../../02-developer-guide/backend/observability.md) |

扩展处理器由受信任的应用代码静态注册，上传的插件包或制品不能作为服务器 Python 扩展被动态执行。

## API 兼容原则

公共、管理、发布、下载及旧路径适配器都有消费者，变更时核对 WPF 客户端、React 客户端和发布脚本。保留仍被依赖的默认字段与错误语义；需要减少大响应时可增加显式 compact/分页视图。总量、当前页条数和 `hasMore`/游标须有准确含义。

不能兼容的新增表示应明确版本边界，例如 `/api/v1/...`；当前并非所有 API 已统一版本化。后端契约、前端请求代码/类型与相关测试需在同一变更中同步。各接口实际采用 SQL 分页、先构造再裁剪或文件回退，由[公共读模型](../../02-developer-guide/backend/public-data.md)和[插件目录](../../02-developer-guide/backend/plugin-catalog.md)说明，不能仅从 `compact` 名字推定效率。

## 性能预算与现有检查

开发时避免把制品哈希、全量目录扫描、索引重建或统计事务阻塞放进公共请求关键路径。当前部分回退仍有磁盘读取、哈希或存储修复；这是一项演进约束，不能当成所有 GET 的现状保证。

| 检查或目标 | 范围 |
| --- | --- |
| **已接入构建：管理首页 gzip ≤ 450 KiB** | `check-dashboard-bundle.mjs` 从 Vite manifest 的 `index.html`、`AdminLayout.tsx`、`Dashboard.tsx` 三个根追踪静态 imports，按去重后的文件统计 `.gz` 大小；没有变体时计原始字节，超限失败 |
| 静态压缩校验 | `precompress-static.mjs` 为可压缩的 HTML/CSS/JS/JSON/SVG 生成更小的 gzip/Brotli 文件，并解压比对原文；这是文件正确性检查 |
| 设计预算：非分页 JSON ≤ 256 KiB | 针对会随数据增长的响应，需用实际接口样本或 focused benchmark 验证 |
| 设计预算：公共入口 preload ≤ 550 KiB gzip | 与管理首页 450 KiB 的根集合不同，不能混作同一个构建门禁 |
| 设计预算：请求内同步文件系统工作 ≤ 50 ms | 需在明确硬件、目录规模和冷/热状态下测量，构建通过不证明已达到 |

管理首页检查不会自动计入未作为根的动态路由，也不包括 CSS、图片、接口数据或完整浏览器加载时间。它是静态模块依赖预算。超出设计预算时应说明实测原因并给出针对性的回归证据，不能把历史数字当成当前结果。

前端使用按路由懒加载和请求取消来控制入口体积及过期响应；静态变体的 HTTP 协商归 `frontend_spa.py`。性能复测需分别记录表示字节数、冷/热耗时和真实页面加载行为。历史测量见[Web 性能基线](../../02-developer-guide/backend/performance-baseline.md)。

## 验证范围

在隔离环境核对运行时权限和实际下载。前端构建只写本地 `dist`。

```powershell
Push-Location Web\Frontend
try {
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw 'Frontend lint failed.' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
}
finally { Pop-Location }
```

性能敏感变更按相同环境和数据复测流式交付、存储一致性或浏览器行为。
