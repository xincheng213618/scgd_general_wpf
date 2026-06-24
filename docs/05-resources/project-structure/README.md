# 项目结构总览

本文档用于快速说明当前仓库主目录分工，并给出每类工作的第一阅读入口。

## 主目录分区

| 目录 | 作用 | 建议先看 |
| --- | --- | --- |
| `ColorVision/` | 主 WPF 应用入口与主窗口 | [安装与首次使用](../../00-getting-started/README.md) / [主窗口导览](../../01-user-guide/interface/main-window.md) |
| `UI/` | UI 类库、主题、属性编辑器、图像编辑器、DLL/NuGet 发布 | [UI 组件与 DLL 发布](../../04-api-reference/ui-components/README.md) |
| `Engine/` | 设备服务、模板系统、流程执行、MQTT、结果处理 | [Engine 组件总览](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | 当前通用插件 | [现有插件能力说明](../../04-api-reference/plugins/README.md) |
| `Projects/` | 客户项目包、业务定制和对接示例 | [项目说明](../../00-projects/README.md) |
| `Web/Backend/` | 插件市场后端服务 | [插件市场后端](../../02-developer-guide/backend/README.md) |
| `Scripts/` | 构建、打包、发布脚本 | [构建与发布脚本](../../02-developer-guide/scripts/README.md) |
| `src/ColorVisionSetup/` | 安装器与更新程序 | [自动更新系统](../../02-developer-guide/deployment/auto-update.md) |
| `Test/` | xUnit、native helper、后端和脚本验证 | [测试与验证](../../02-developer-guide/testing.md) |
| `docs/` | VitePress 文档源码 | 当前文档 |

## 按角色阅读

### 新用户或实施人员

1. [项目说明](../../00-projects/README.md)
2. [使用手册](../../01-user-guide/README.md)
3. [常见问题](../../01-user-guide/troubleshooting/common-issues.md)

### Engine 或算法开发

1. [Engine 组件总览](../../04-api-reference/engine-components/README.md)
2. [Engine 开发指南](../../02-developer-guide/engine-development/README.md)
3. [算法与模板](../../04-api-reference/algorithms/README.md)

### UI 发布维护

1. [UI 组件与 DLL 发布](../../04-api-reference/ui-components/README.md)
2. [UI DLL 发布](../../04-api-reference/ui-components/publishing.md)

### 插件开发

1. [扩展性概览](../../02-developer-guide/core-concepts/extensibility.md)
2. [插件开发手册](../../02-developer-guide/plugin-development/README.md)
3. [现有插件能力说明](../../04-api-reference/plugins/README.md)

### 客户项目维护

1. [项目说明](../../00-projects/README.md)
2. 对应项目页
3. 项目目录内 README / CHANGELOG

### 文档维护

1. [附录与资源](../README.md)
2. `docs/.vitepress/i18n/navigation-data.json`

## 说明

- 这里提供的是“从哪里开始看”的入口，不替代详细专题页。
- 历史插件或旧方案页面只作为说明，不作为当前功能入口。
- 新增源码模块后，应优先更新本页和对应章节首页，不再维护额外的全量对照表。
