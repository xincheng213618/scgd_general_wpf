# 项目结构总览

本文档用于快速说明当前仓库的主目录分工，并给出每个目录最合适的文档入口。

## 主目录分区

| 目录 | 作用 | 建议先看 |
| --- | --- | --- |
| `ColorVision/` | 主 WPF 应用入口与主窗口 | [入门指南](../../00-getting-started/README.md) / [主窗口导览](../../01-user-guide/interface/main-window.md) |
| `UI/` | UI 框架、主题、属性编辑器、图像编辑器 | [UI 组件总览](../../04-api-reference/ui-components/README.md) |
| `Engine/` | 核心引擎、设备服务、模板系统、流程执行 | [Engine 开发指南](../../02-developer-guide/engine-development/README.md) / [Engine 组件总览](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | 运行时插件和扩展能力 | [插件开发概览](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | 客户项目包和业务定制实现 | [组件交互](../../03-architecture/overview/component-interactions.md) |
| `Backend/marketplace/` | 插件市场后端服务 | [插件市场后端](../../02-developer-guide/backend/README.md) |
| `Scripts/` | 构建、打包、发布脚本 | [构建与发布脚本](../../02-developer-guide/scripts/README.md) |
| `ColorVisionSetup/` | 安装器与更新程序 | [自动更新系统](../../02-developer-guide/deployment/auto-update.md) |
| `Test/` | 测试项目 | [开发指南](../../02-developer-guide/README.md) |
| `docs/` | VitePress 文档源码 | 当前文档 / [模块与文档对照表](./module-documentation-map.md) |

## 按角色阅读

### 新用户或实施同学

1. [入门指南](../../00-getting-started/README.md)
2. [用户指南](../../01-user-guide/README.md)
3. [常见问题](../../01-user-guide/troubleshooting/common-issues.md)

### 引擎或算法开发

1. [架构设计](../../03-architecture/README.md)
2. [Engine 开发指南](../../02-developer-guide/engine-development/README.md)
3. [算法总览](../../04-api-reference/algorithms/README.md)

### 插件开发

1. [扩展性概览](../../02-developer-guide/core-concepts/extensibility.md)
2. [插件开发概览](../../02-developer-guide/plugin-development/overview.md)
3. [标准插件专题](../../04-api-reference/plugins/standard-plugins/pattern.md)

### 文档维护

1. [附录与资源](../README.md)
2. [模块与文档对照表](./module-documentation-map.md)

## 说明

- 这里提供的是“从哪里开始看”的入口，不替代详细 API 或专题页。
- 若某个目录缺少独立文档，优先从相邻章节的总览页进入，而不是继续扩散新的散页索引。