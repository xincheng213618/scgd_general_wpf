---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Web 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## Web/ 根目录与跨模块关联 {#module-576562}

- [审计与部署记录：来源、查询与证据边界](../../02-developer-guide/backend/management-records.md) — `delivery.backend-records`
  Backend审计与NAS部署历史的来源、筛选total与summary统计、展示脱敏和保留边界；空审计可能是查询失败，历史成功不证明当前服务或完整恢复。

- [Web 本地启动与 NAS 部署](../../02-developer-guide/deployment/web.md) — `delivery.web-deployment`
  Web 本地启动与 Windows NAS 部署的前提、参数、构建和健康检查、Git bundle 交付、备份保留及失败恢复；SkipTests 仍运行前端测试，失败不保证回退代码或数据库。

- [Web 历史性能基线 \[历史\]](../../02-developer-guide/backend/performance-baseline.md) — `delivery.web-performance-baseline`
  Web 的 2026-07-18 基线及后续补充测量，保留样本、方法和比较数字；不是当前部署性能、容量或测试状态。

## Web/Backend {#module-5765622f4261636b656e64}

- [Android 运维伴侣](../../02-developer-guide/backend/android-operations.md) — `delivery.android-operations`
  Android原生运维入口、现场HTTPS与固定签名中继的职责边界；连接、可见证据和操作授权不能互相替代。

- [HTTP 制品交付、完成计数与响应策略](../../02-developer-guide/backend/artifact-delivery.md) — `delivery.artifact-delivery`
  Backend HTTP制品交付的Range、完成事件、下载计数、Cache-Control/ETag、HEAD副作用与JSON gzip边界；服务端迭代完成不证明客户端落盘。

- [插件市场后端](../../02-developer-guide/backend/README.md) — `delivery.backend`
  Flask 后端的组成、配置、CLI 参数、管理入口与探测边界；--storage 不隔离配置或 SQLite，命令退出 0 仍须核对业务结果。

- [Web账号、角色与会话生命周期](../../02-developer-guide/backend/accounts.md) — `delivery.backend-accounts`
  Backend注册、角色权限、改密与找回、数据库Session撤销；配置管理员不走auth\_version，跨服务安全操作可能部分成功。

- [HTTP认证、API key与浏览器CSRF](../../02-developer-guide/backend/authentication.md) — `delivery.backend-auth`
  Backend HTTP凭据优先级、Session权限与API key scopes、key轮换失败副作用和浏览器CSRF；认证成功不等于端点授权或全流程完成。

- [Backend Copilot配置管理与敏感配置交付](../../02-developer-guide/backend/copilot-sync.md) — `delivery.backend-copilot-sync`
  Backend Copilot配置管理、AES-GCM密钥存储与全量同步；版本HMAC不是独立设备身份，nonce不去重，成功读取会交付provider秘密。

- [Backend反馈提交、处理状态与附件访问](../../02-developer-guide/backend/feedback.md) — `delivery.backend-feedback`
  Backend公开反馈提交、文件目录收件箱、状态sidecar和受控附件响应；上传与管理校验不同，201、resolved及下载审计各有完成边界。

- [Backend内置任务、执行记录与恢复](../../02-developer-guide/backend/jobs.md) — `delivery.backend-jobs`
  Backend内置任务的后台轮询、同步手动执行、SQLite单飞和启动恢复；禁用/停止不取消运行中handler，任务返回不证明历史落盘或副作用回滚。

- [访问统计、浏览器体验与性能观测](../../02-developer-guide/backend/observability.md) — `delivery.backend-observability`
  Backend HTTP访问、SPA体验与性能观测的统计口径、异步丢事件、每日HMAC关联边界、日界线和进程缓存；不等于真实人数或下载完成。

- [Backend Operations 中继与只读概览](../../02-developer-guide/backend/operations-relay.md) — `delivery.backend-operations`
  Backend Operations 的 Bearer 与设备签名中继、任务回执和管理员只读投影；在线、排队、验签与真实动作完成各有边界。

- [公共站点数据、分页与文件可见性](../../02-developer-guide/backend/public-data.md) — `delivery.backend-public-data`
  公共站点首页、发行归档、日志、工具、Android更新和目录浏览的读模型；compact路径不同，GET可写缓存或修复旧更新目录。

- [审计与部署记录：来源、查询与证据边界](../../02-developer-guide/backend/management-records.md) — `delivery.backend-records`
  Backend审计与NAS部署历史的来源、筛选total与summary统计、展示脱敏和保留边界；空审计可能是查询失败，历史成功不证明当前服务或完整恢复。

- [Backend保留配置、数据库快照与隐私清理](../../02-developer-guide/backend/backup-retention.md) — `delivery.backend-retention`
  Backend六项在线保留配置、SQLite快照创建、隐私清理和轮换；原子配置替换不等于全链事务，备份存在也不证明所有清理或恢复安全。

- [CVWindowsService 服务包发布与选择](../../02-developer-guide/backend/cvwindowsservice.md) — `delivery.cvwindowsservice`
  CVWindowsService 服务包的发布、LATEST\_RELEASE、缓存与按版本选包；文件名通过不证明ZIP有效，发布不等于本机安装。

- [文件中转、覆盖与公开分享](../../02-developer-guide/backend/file-transfer.md) — `delivery.file-transfer`
  Web文件中转的上传、取消与续传、完成判定、权限、覆盖和公开分享；队列不持久化，分享绑定当前文件名。

- [插件目录、详情投影与索引刷新](../../02-developer-guide/backend/plugin-catalog.md) — `delivery.plugin-catalog`
  插件市场列表、详情投影、索引刷新与版本缓存；compact不代表按页读取源码数据，ready不证明全量刷新无错误。

- [Web 页面与文档托管](../../02-developer-guide/backend/web-pages.md) — `delivery.web-pages`
  React 页面和 VitePress 文档的托管、开发代理、缓存、Brotli/gzip 与 Range 协商、旧分块恢复及文档索引；后台索引刷新不构建网页，也不证明已部署内容最新。

- [Web 架构与演进边界](../../03-architecture/components/web.md) — `platform.web-architecture`
  Web 的组成根、HTTP/服务/持久化边界、现有接口和架构检查；区分已实现约束、性能预算与后续演进目标。

- [Web 本地启动与 NAS 部署](../../02-developer-guide/deployment/web.md) — `delivery.web-deployment`
  Web 本地启动与 Windows NAS 部署的前提、参数、构建和健康检查、Git bundle 交付、备份保留及失败恢复；SkipTests 仍运行前端测试，失败不保证回退代码或数据库。

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。

## Web/Frontend {#module-5765622f46726f6e74656e64}

- [插件市场后端](../../02-developer-guide/backend/README.md) — `delivery.backend`
  Flask 后端的组成、配置、CLI 参数、管理入口与探测边界；--storage 不隔离配置或 SQLite，命令退出 0 仍须核对业务结果。

- [Backend Copilot配置管理与敏感配置交付](../../02-developer-guide/backend/copilot-sync.md) — `delivery.backend-copilot-sync`
  Backend Copilot配置管理、AES-GCM密钥存储与全量同步；版本HMAC不是独立设备身份，nonce不去重，成功读取会交付provider秘密。

- [Backend反馈提交、处理状态与附件访问](../../02-developer-guide/backend/feedback.md) — `delivery.backend-feedback`
  Backend公开反馈提交、文件目录收件箱、状态sidecar和受控附件响应；上传与管理校验不同，201、resolved及下载审计各有完成边界。

- [访问统计、浏览器体验与性能观测](../../02-developer-guide/backend/observability.md) — `delivery.backend-observability`
  Backend HTTP访问、SPA体验与性能观测的统计口径、异步丢事件、每日HMAC关联边界、日界线和进程缓存；不等于真实人数或下载完成。

- [审计与部署记录：来源、查询与证据边界](../../02-developer-guide/backend/management-records.md) — `delivery.backend-records`
  Backend审计与NAS部署历史的来源、筛选total与summary统计、展示脱敏和保留边界；空审计可能是查询失败，历史成功不证明当前服务或完整恢复。

- [Backend保留配置、数据库快照与隐私清理](../../02-developer-guide/backend/backup-retention.md) — `delivery.backend-retention`
  Backend六项在线保留配置、SQLite快照创建、隐私清理和轮换；原子配置替换不等于全链事务，备份存在也不证明所有清理或恢复安全。

- [CVWindowsService 服务包发布与选择](../../02-developer-guide/backend/cvwindowsservice.md) — `delivery.cvwindowsservice`
  CVWindowsService 服务包的发布、LATEST\_RELEASE、缓存与按版本选包；文件名通过不证明ZIP有效，发布不等于本机安装。

- [文件中转、覆盖与公开分享](../../02-developer-guide/backend/file-transfer.md) — `delivery.file-transfer`
  Web文件中转的上传、取消与续传、完成判定、权限、覆盖和公开分享；队列不持久化，分享绑定当前文件名。

- [Web 页面与文档托管](../../02-developer-guide/backend/web-pages.md) — `delivery.web-pages`
  React 页面和 VitePress 文档的托管、开发代理、缓存、Brotli/gzip 与 Range 协商、旧分块恢复及文档索引；后台索引刷新不构建网页，也不证明已部署内容最新。

- [Web 架构与演进边界](../../03-architecture/components/web.md) — `platform.web-architecture`
  Web 的组成根、HTTP/服务/持久化边界、现有接口和架构检查；区分已实现约束、性能预算与后续演进目标。

- [Web 本地启动与 NAS 部署](../../02-developer-guide/deployment/web.md) — `delivery.web-deployment`
  Web 本地启动与 Windows NAS 部署的前提、参数、构建和健康检查、Git bundle 交付、备份保留及失败恢复；SkipTests 仍运行前端测试，失败不保证回退代码或数据库。
