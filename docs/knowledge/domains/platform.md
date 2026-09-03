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

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  按插件、菜单、属性面板、设备、Flow、算法和结果扩展任务定位实现与当前契约。

- [系统开发工具管理](../../02-developer-guide/core-concepts/developer-tools-manager.md) — `platform.developer-tools`
  独立开发工具窗口发现系统 Python、Node.js/npm，并由用户选择校验后启动官方安装向导；不托管项目环境，不自动改默认版本。

- [ColorVisionDriver：实验性内核驱动骨架](../../03-architecture/components/kernel-driver.md) — `platform.kernel-driver`
  ColorVisionDriver 实验性 WDM 驱动骨架的两个 IOCTL、WDK 构建输入与接入边界；尚未接入主程序、服务宿主或正式发布链。

- [RBAC：登录缓存、会话与权限边界](../../03-architecture/security/rbac.md) — `platform.rbac`
  本地RBAC的登录缓存、会话校验和权限同步限制，以及自动登录失败、登出撤销和用户中心统计的实际边界。

- [启动、初始化与故障恢复](../../03-architecture/overview/runtime.md) — `platform.runtime`
  启动顺序与故障恢复：初始化进度和ready不代表全部成功，运行期维护区分浏览、禁用、文档准备与重启，一次性插件跳过不绕过真实故障。

- [权限边界与鉴权入口](../../03-architecture/security/overview.md) — `platform.security`
  区分应用管理员、RBAC会话与权限码、Windows服务身份及远程/工具授权；登录缓存和界面状态不能替代执行入口的权限检查。

- [ColorVisionServiceHost：本机权限代理与生命周期](../../03-architecture/components/service-host.md) — `platform.service-host`
  ColorVision 服务主机的状态刷新、安装修复、日志诊断、身份票据与就绪条件；自动刷新只更新日志，客户端超时不取消命令，服务停止超过两分钟仍等待排空，服务启动成功日志不证明后台清理和启动完整性检查完成。

- [启动失败上报与缺依赖告警](../../03-architecture/components/startup-integrity.md) — `platform.startup-integrity`
  主程序启动失败识别、状态上报和后台缺依赖告警；十秒观察不强杀进程，已处理终态抑制重复弹窗，无告警不证明安装完整。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [Web 架构与演进边界](../../03-architecture/components/web.md) — `platform.web-architecture`
  Web 的组成根、HTTP/服务/持久化边界、现有接口和架构检查；区分已实现约束、性能预算与后续演进目标。

- [软件许可协议](../../05-resources/legal/software-agreement.md) — `platform.license`
  保留软件许可协议原文供定位，不由AI重新解释或改写许可条款。

- [ColorVision 概览](../../00-getting-started/what-is-colorvision.md) — `platform.product`
  ColorVision 的设备、流程、图像分析、结果、插件与客户项目能力，以及从任务进入文档的方法。
