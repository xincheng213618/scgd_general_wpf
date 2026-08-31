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
## 目录说明

- `overview/` 关注系统级视角，例如启动、运行时和组件关系。
- `components/engine/` 关注流程引擎与执行模型。
- `components/templates/` 维护模板核心契约；旧分析地址仅作兼容跳转。
- `security/` 关注权限模型和安全边界。

## 建议怎么读

- 不确定范围时先查[知识地图](../knowledge/index.md)，再核对主题关联的源码与测试。
- 需要修改流程或模板时，再进入 `components/` 下的专题页。
- 需要接口和类型细节时，从[生成的源码地图](../knowledge/index.md)直接定位主题及其源码。
