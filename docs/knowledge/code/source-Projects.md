---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Projects 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## Projects/ 根目录与跨模块关联 {#module-50726f6a65637473}

- [客户项目与对接示例入口](../../04-api-reference/projects/README.md) — `projects.index`
  按客户业务代码、独立对接示例与构建发布边界定位 Projects 的权威主题。

## Projects/ProjectARVRPro {#module-50726f6a656374732f50726f6a6563744152565250726f}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [配置 ARVRPro 流程、解析映射与 Recipe](../../04-api-reference/projects/project-arvr-pro-processes.md) — `projects.arvr-pro-processes`
  配置 ARVRPro 流程组、流程解析映射、实例 Recipe 与雷鸟切图，说明类型选择、结果快照、配置保存和有效迁移规则。

- [ProjectARVRPro](../../04-api-reference/projects/project-arvr-pro.md) — `projects.arvr-pro`
  ARVRPro 项目入口、Socket 自动化、输出与历史结果查询；流程组、实例 Recipe 和 Demura 各有对应操作主题。

- [ProjectARVRPro.IntegrationDemo](../../04-api-reference/projects/project-arvr-pro-integration-demo.md) — `projects.arvr-pro-demo`
  独立 net48 ARVRPro TCP/JSON Demo 的公开字段、ACK 与最终完成判据、半包粘包及离线验证。

- [Demura 烧录与 PG 通信](../../04-api-reference/projects/project-arvr-pro-demura.md) — `projects.arvr-pro-demura`
  ProjectARVRPro Demura 烧录的 PG TCP 连接、GECS 帧、配置默认值、逐步回包和故障定位；写入成功回包不等于光学效果验收。

- [ARVRPro TCP 通讯协议](../../04-api-reference/projects/project-arvr-pro-protocol.md) — `projects.arvr-pro-protocol`
  ARVRPro TCP/JSON 对接：初始化与 RunAll、流程启用设置、切图确认、AOI 中转、状态码和最终结果关联；说明分帧与并发会话限制。

- [项目横向速查](../../04-api-reference/projects/project-capability-matrix.md) — `projects.capabilities`
  按协议、外部触发、结果出口与最小验证路径比较 ARVRPro、KB、LUX 和 IntegrationDemo。

## Projects/ProjectARVRPro.IntegrationDemo {#module-50726f6a656374732f50726f6a6563744152565250726f2e496e746567726174696f6e44656d6f}

- [ProjectARVRPro.IntegrationDemo](../../04-api-reference/projects/project-arvr-pro-integration-demo.md) — `projects.arvr-pro-demo`
  独立 net48 ARVRPro TCP/JSON Demo 的公开字段、ACK 与最终完成判据、半包粘包及离线验证。

- [项目横向速查](../../04-api-reference/projects/project-capability-matrix.md) — `projects.capabilities`
  按协议、外部触发、结果出口与最小验证路径比较 ARVRPro、KB、LUX 和 IntegrationDemo。

## Projects/ProjectKB {#module-50726f6a656374732f50726f6a6563744b42}

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [项目横向速查](../../04-api-reference/projects/project-capability-matrix.md) — `projects.capabilities`
  按协议、外部触发、结果出口与最小验证路径比较 ARVRPro、KB、LUX 和 IntegrationDemo。

- [ProjectKB](../../04-api-reference/projects/project-kb.md) — `projects.kb`
  ProjectKB 的宿主/独立启动依赖、Modbus/MES、Recipe 判定、背光修正、CSV 与按天生产统计和运行内查询记忆。

## Projects/ProjectLUX {#module-50726f6a656374732f50726f6a6563744c5558}

- [项目横向速查](../../04-api-reference/projects/project-capability-matrix.md) — `projects.capabilities`
  按协议、外部触发、结果出口与最小验证路径比较 ARVRPro、KB、LUX 和 IntegrationDemo。

- [ProjectLUX](../../04-api-reference/projects/project-lux.md) — `projects.lux`
  ProjectLUX 文本 Socket、ProcessGroup、Recipe/Fix 与 CSV/SQLite 结果链及构建发布边界。
