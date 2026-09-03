---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# src 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## src/ColorVisionServiceHost {#module-7372632f436f6c6f72566973696f6e53657276696365486f7374}

- [更新扫描保护：临时排除项与清理所有权](../../02-developer-guide/deployment/update-scan-protection.md) — `delivery.update-scan-protection`
  ServiceHost提供的主程序增量更新临时Defender排除项、目录准入和清理所有权；启用失败不阻断更新，服务停止或保护超时不保证排除项立即恢复。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [ColorVisionServiceHost：本机权限代理与生命周期](../../03-architecture/components/service-host.md) — `platform.service-host`
  ColorVision 服务主机的状态刷新、安装修复、日志诊断、身份票据与就绪条件；自动刷新只更新日志，客户端超时不取消命令，服务停止超过两分钟仍等待排空，服务启动成功日志不证明后台清理和启动完整性检查完成。

- [启动失败上报与缺依赖告警](../../03-architecture/components/startup-integrity.md) — `platform.startup-integrity`
  主程序启动失败识别、状态上报和后台缺依赖告警；十秒观察不强杀进程，已处理终态抑制重复弹窗，无告警不证明安装完整。

## src/ColorVisionSetup {#module-7372632f436f6c6f72566973696f6e5365747570}

- [桌面交付制品与责任路由](../../02-developer-guide/deployment/overview.md) — `delivery.deployment`
  按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。
