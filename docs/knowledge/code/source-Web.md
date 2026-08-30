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

## Web/Backend {#module-5765622f4261636b656e64}

- [Android 运维伴侣](../../02-developer-guide/backend/android-operations.md) — `delivery.android-operations`
  Android原生运维入口、现场HTTPS与固定签名中继的职责边界；连接、可见证据和操作授权不能互相替代。

- [HTTP 制品交付、完成计数与响应策略](../../02-developer-guide/backend/artifact-delivery.md) — `delivery.artifact-delivery`
  Backend HTTP制品交付的Range、完成事件、下载计数、Cache-Control/ETag、HEAD副作用与JSON gzip边界；服务端迭代完成不证明客户端落盘。

- [插件市场后端](../../02-developer-guide/backend/README.md) — `delivery.backend`
  Flask后端的组成、配置、制品与数据库路径、认证和探测边界；--storage不隔离配置或SQLite。

- [Web账号、角色与会话生命周期](../../02-developer-guide/backend/accounts.md) — `delivery.backend-accounts`
  Backend注册、角色权限、改密与找回、数据库Session撤销；配置管理员不走auth\_version，跨服务安全操作可能部分成功。

- [HTTP认证、API key与浏览器CSRF](../../02-developer-guide/backend/authentication.md) — `delivery.backend-auth`
  Backend HTTP凭据优先级、Session权限与API key scopes、key轮换失败副作用和浏览器CSRF；认证成功不等于端点授权或全流程完成。

- [文件中转、覆盖与公开分享](../../02-developer-guide/backend/file-transfer.md) — `delivery.file-transfer`
  Backend文件中转的整文件与断点上传、权限、覆盖、公开分享及到期删除；分享绑定文件名而非不可变上传版本。

- [插件目录、详情投影与索引刷新](../../02-developer-guide/backend/plugin-catalog.md) — `delivery.plugin-catalog`
  插件市场列表、详情投影、索引刷新与版本缓存；compact不代表按页读取源码数据，ready不证明全量刷新无错误。

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。
