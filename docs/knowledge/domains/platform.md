---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 平台与架构

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

宿主架构、模块责任、扩展分流与权限边界。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [架构设计](../../03-architecture/README.md) — `platform.architecture`
  按启动、跨模块调用、流程、模板与权限问题定位架构契约。

- [扩展任务入口](../../04-api-reference/extensions/README.md) — `platform.extensions`
  按 Flow 节点、属性编辑器、模板、设备和插件问题定位可复用扩展契约。

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  菜单、插件、属性编辑器、算法模板和 Copilot 扩展的职责与源码入口。

- [RBAC：登录缓存、会话与权限边界](../../03-architecture/security/rbac.md) — `platform.rbac`
  本地RBAC的登录缓存、会话校验和权限同步限制，以及自动登录失败、登出撤销和用户中心统计的实际边界。

- [架构运行时](../../03-architecture/overview/runtime.md) — `platform.runtime`
  启动分支、配置初始化、插件装载和恢复流程的运行时顺序。

- [安全与权限控制](../../03-architecture/security/overview.md) — `platform.security`
  区分全局粗粒度权限和独立RBAC模块，不承诺不存在的统一业务授权边界。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [软件许可协议](../../05-resources/legal/software-agreement.md) — `platform.license`
  保留软件许可协议原文供定位，不由AI重新解释或改写许可条款。

- [什么是 ColorVision？](../../00-getting-started/what-is-colorvision.md) — `platform.product`
  定位ColorVision视觉检测平台的业务场景和主要职责，不代替具体能力契约。
