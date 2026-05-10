# 开发指南

本章节聚焦二次开发、扩展点和交付流程；类库细节和模块设计请分别进入 API 参考与架构设计。

## 从这里开始的常见场景

### 理解扩展机制

- [扩展性概览](./core-concepts/extensibility.md)

### 修改 Engine 或模板相关功能

- [Engine 开发指南](./engine-development/README.md)
- [架构设计](../03-architecture/README.md)
- [Engine 组件 API](../04-api-reference/engine-components/README.md)

### 开发插件

- [插件开发总览](./plugin-development/README.md)
- [插件开发入门](./plugin-development/getting-started.md)
- [插件生命周期](./plugin-development/lifecycle.md)

### 构建、部署与更新

- [部署概览](./deployment/overview.md)
- [自动更新系统](./deployment/auto-update.md)
- [构建与发布脚本](./scripts/README.md)

### 后端与辅助系统

- [插件市场后端](./backend/README.md)
- [性能优化概览](./performance/overview.md)

## 推荐阅读路径

1. 先看 [架构设计](../03-architecture/README.md)，确认模块边界。
2. 再看 [扩展性概览](./core-concepts/extensibility.md)，确认扩展点和插件入口。
3. 进入自己的目标专题：插件、Engine、部署或后端。
4. 需要类和接口细节时，转到 [API 参考](../04-api-reference/README.md)。

## 章节边界

- 本章节优先提供“怎么进入代码”的路径，而不是替代 API 手册。
- Engine 子目录中的部分细分专题仍在收敛中，因此默认入口改为总览页，避免把未维护的小页继续放在主导航。
- 与 AI/Agent 相关的试验性材料保留在子目录中，但不再作为默认阅读路径。

## 补充入口

- [项目结构总览](../05-resources/project-structure/README.md)
- [在线仓库](https://github.com/xincheng213618/scgd_general_wpf)
- [问题跟踪](https://github.com/xincheng213618/scgd_general_wpf/issues)
