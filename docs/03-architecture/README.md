---
knowledge_id: "platform.architecture"
knowledge_type: "index"
status: "current"
summary: "按启动、跨模块调用、流程、模板与权限问题定位架构契约。"
aliases: ["架构设计","模块关系"]
code_paths: ["ColorVision/App.xaml.cs","Engine/ColorVision.Engine/FlowProcessing"]
test_paths: []
related: ["platform.system","platform.runtime","flow.architecture"]
---

# 架构设计

这里索引当前架构契约。先定位职责与调用链，只读取与任务相关的主题；不用先完成一条固定阅读路线。

## 按问题定位

| 问题 | 主题 |
| --- | --- |
| 哪个模块负责这件事，跨模块关系怎样确认？ | [系统职责与调用边界](./overview/system-overview.md) |
| 启动、插件装载或恢复为什么异常？ | [运行时](./overview/runtime.md) |
| 节点结束、整图结束和后处理是什么关系？ | [Flow架构](./components/engine/flow-engine.md) |
| 模板如何注册和持久化？ | [模板核心契约](./components/templates/design.md) |
| 模板编辑、新建和关闭意味着什么？ | [编辑与创建宿主](../04-api-reference/algorithms/templates/template-management.md) |
| Flow 模板如何处理并发保存和关联包？ | [Flow 模板契约](../04-api-reference/engine-components/template-flow-chain.md) |
| 哪些授权真正存在，哪些没有统一接入？ | [权限边界](./security/overview.md)、[RBAC](./security/rbac.md) |

具体组件及源码关联见[生成的知识地图](../knowledge/index.md)。修改前核对目标主题的实现、测试和验证缺口；编号目录保留为稳定地址，不另规定阅读顺序。
