---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 项目知识地图

> 由 Markdown 元数据生成。不要手工编辑；在仓库根目录运行 `node docs/.vitepress/scripts/knowledge.mjs generate`。

从现有 `AGENTS.md` 读取工作约束，再按源码职责进入模块；索引只负责定位，修改前核对正文、关联源码及测试。`规划`、`历史`不是当前能力。

离线检索：`node docs/.vitepress/scripts/knowledge.mjs search "问题或代码符号"`；反向映射：`node docs/.vitepress/scripts/knowledge.mjs impact "仓库相对路径"`。

共 159 个主题；默认 CLI 搜索只返回 current，使用 `--all` 明确包含规划与历史。

## 按源码根与模块定位

分组由主题的 `code_paths` 和真实目录派生；同一主题可关联多个模块，各组数量不能相加。关联不等于完整调用图；仅引用源码根的概览不会扩散到每个子模块。

| 源码根 | 目录分组 | 关联主题 |
| --- | ---: | ---: |
| [ColorVision](./code/source-ColorVision.md) | 8 | 35 |
| [UI](./code/source-UI.md) | 14 | 80 |
| [Engine](./code/source-Engine.md) | 6 | 72 |
| [Native](./code/source-Native.md) | 4 | 5 |
| [Plugins](./code/source-Plugins.md) | 5 | 11 |
| [Projects](./code/source-Projects.md) | 5 | 8 |
| [Web](./code/source-Web.md) | 1 | 4 |
| [Scripts](./code/source-Scripts.md) | 2 | 10 |
| [Test](./code/source-Test.md) | 1 | 1 |
| [AndroidWebViewApp](./code/source-AndroidWebViewApp.md) | 2 | 1 |
| [SDK](./code/source-SDK.md) | 1 | 1 |
| [src](./code/source-src.md) | 2 | 2 |
| [仓库与知识基础设施](./code/repository.md) | 5 | 15 |

## 按能力领域补充检索

跨源码模块的问题仍可从能力领域进入；这不是另一套按读者身份编排的手册。

- [AI 共治与知识维护](./domains/governance.md) — 4 个主题；工作边界、知识维护、文档与源码冲突、检索验收。
- [平台与架构](./domains/platform.md) — 9 个主题；宿主架构、模块责任、扩展分流与权限边界。
- [UI 与图像交互](./domains/ui.md) — 30 个主题；属性编辑器、窗口组件、图像交互和绘制扩展。
- [设备、服务与结果](./domains/engine.md) — 16 个主题；设备服务、MQTT、模板宿主和结果展示。
- [流程编排与执行](./domains/flow.md) — 10 个主题；流程编辑、节点运行、参数传递与完成语义。
- [算法与模板](./domains/algorithms.md) — 36 个主题；算法平台、传统模板、计算适配和规划中的能力。
- [Copilot](./domains/copilot.md) — 10 个主题；Agent会话、工具契约、上下文、恢复和MCP边界。
- [客户项目](./domains/projects.md) — 6 个主题；客户包、业务流程、协议对接与结果留存。
- [插件与扩展](./domains/plugins.md) — 10 个主题；插件发现、生命周期、已有插件和集成边界。
- [构建、测试与交付](./domains/delivery.md) — 11 个主题；克隆环境、构建依赖、测试、发布脚本和更新。
- [运行与现场排查](./domains/operations.md) — 17 个主题；安装使用、设备配置、现场故障、日志和数据管理。
