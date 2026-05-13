# 模块与文档对照表

本文档只保留当前仓库结构和仍然有效的文档入口，用于快速定位“代码在哪，先看什么文档”。

## 代码区域到文档入口

| 代码区域 | 关注点 | 首选文档入口 | 补充入口 |
| --- | --- | --- | --- |
| `ColorVision/` | 主程序入口、主窗口、应用启动 | [入门指南](../../00-getting-started/README.md) | [主窗口导览](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI 框架、主题、编辑器 | [UI 组件总览](../../04-api-reference/ui-components/README.md) | [用户指南](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCP 服务、JSON/Text 分发、消息历史、管理窗口 | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [Socket 通信模块优化路线](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` | 设备服务、服务协调 | [设备服务概览](../../01-user-guide/devices/overview.md) | [Engine 开发指南](../../02-developer-guide/engine-development/README.md) |
| `Engine/ColorVision.Engine/Templates/` | 模板系统、参数化算法、结果处理 | [算法总览](../../04-api-reference/algorithms/README.md) | [Templates 架构设计](../../03-architecture/components/templates/design.md) |
| `Engine/FlowEngineLib/` | 流程节点、执行模型、可视化流程 | [FlowEngineLib 架构](../../03-architecture/components/engine/flow-engine.md) | [FlowNode 开发](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV 集成、底层视觉处理 | [Engine 组件总览](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Plugins/` | 运行时插件和扩展能力 | [插件开发概览](../../02-developer-guide/plugin-development/overview.md) | [标准插件专题](../../04-api-reference/plugins/standard-plugins/pattern.md) |
| `Projects/` | 客户项目、定制业务拼装 | [组件交互](../../03-architecture/overview/component-interactions.md) | [项目结构总览](./README.md) |
| `ColorVisionSetup/` | 安装器和更新流程 | [部署概览](../../02-developer-guide/deployment/overview.md) | [自动更新系统](../../02-developer-guide/deployment/auto-update.md) |
| `Backend/marketplace/` | 插件市场后端 | [插件市场后端](../../02-developer-guide/backend/README.md) | [开发指南](../../02-developer-guide/README.md) |
| `Scripts/` | 构建、打包、发布脚本 | [构建与发布脚本](../../02-developer-guide/scripts/README.md) | [部署概览](../../02-developer-guide/deployment/overview.md) |
| `docs/` | 当前文档站源码 | [附录与资源](../README.md) | 当前文档 |

## 按任务查找

### 想新增设备服务

1. 先看 [设备服务概览](../../01-user-guide/devices/overview.md)
2. 再看 [Engine 开发指南](../../02-developer-guide/engine-development/README.md)
3. 最后进入 [Engine 组件总览](../../04-api-reference/engine-components/README.md) 找具体模块页

### 想开发插件

1. [扩展性概览](../../02-developer-guide/core-concepts/extensibility.md)
2. [插件开发概览](../../02-developer-guide/plugin-development/overview.md)
3. [插件开发入门](../../02-developer-guide/plugin-development/getting-started.md)

### 想理解模板或流程

1. [算法总览](../../04-api-reference/algorithms/README.md)
2. [FlowEngineLib 架构](../../03-architecture/components/engine/flow-engine.md)
3. [Templates 架构设计](../../03-architecture/components/templates/design.md)
4. [Templates API 参考](../../04-api-reference/algorithms/templates/api-reference.md)

### 想改 UI 或属性编辑

1. [用户指南](../../01-user-guide/README.md)
2. [UI 组件总览](../../04-api-reference/ui-components/README.md)
3. [属性编辑器](../../01-user-guide/interface/property-editor.md)

### 想看构建、发布和更新

1. [部署概览](../../02-developer-guide/deployment/overview.md)
2. [自动更新系统](../../02-developer-guide/deployment/auto-update.md)
3. [构建与发布脚本](../../02-developer-guide/scripts/README.md)

## 使用原则

- 先从章节首页进入，再跳转到具体专题页。
- 历史草案、孤立文档和旧路径页面不再作为主入口。
- 如果找不到完全对应的专题页，优先回到总览页，而不是继续依赖旧目录命名。
