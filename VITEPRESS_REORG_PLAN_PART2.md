# VitePress 文档结构重组计划（中篇）

## 📋 详细迁移步骤

本文档详细说明每个文件的迁移操作、VitePress 配置更新和实施顺序。

## 🎯 实施阶段划分

### 阶段一：准备工作 ✓

- [ ] 1.1 备份当前 docs 目录
- [ ] 1.2 创建新目录结构
- [ ] 1.3 准备迁移脚本
- [ ] 1.4 建立文档迁移跟踪表

### 阶段二：创建新目录结构

#### 2.1 创建一级目录

```bash
cd /home/runner/work/scgd_general_wpf/scgd_general_wpf/docs

# 创建新的一级分类目录
mkdir -p 00-getting-started
mkdir -p 01-user-guide
mkdir -p 02-developer-guide
mkdir -p 03-architecture
mkdir -p 04-api-reference
mkdir -p 05-resources
```

**任务清单**：
- [ ] 2.1.1 创建 `00-getting-started/` 目录
- [ ] 2.1.2 创建 `01-user-guide/` 目录
- [ ] 2.1.3 创建 `02-developer-guide/` 目录
- [ ] 2.1.4 创建 `03-architecture/` 目录
- [ ] 2.1.5 创建 `04-api-reference/` 目录
- [ ] 2.1.6 创建 `05-resources/` 目录

#### 2.2 创建二级和三级目录

详见 [完整目录清单](#完整目录创建清单)

**任务清单**：
- [ ] 2.2.1 创建 `00-getting-started/` 子目录（无）
- [ ] 2.2.2 创建 `01-user-guide/` 子目录（6个）
- [ ] 2.2.3 创建 `02-developer-guide/` 子目录（8个）
- [ ] 2.2.4 创建 `03-architecture/` 子目录（7个）
- [ ] 2.2.5 创建 `04-api-reference/` 子目录（6个）
- [ ] 2.2.6 创建 `05-resources/` 子目录（6个）

### 阶段三：迁移快速入门文档

#### 3.1 迁移 getting-started 内容

**源目录**: `getting-started/`  
**目标目录**: `00-getting-started/`

| 源文件 | 目标文件 | 操作 | 说明 |
|-------|---------|-----|------|
| `getting-started/入门指南.md` | `00-getting-started/README.md` | 移动+重命名 | 作为总览 |
| `getting-started/quick-start/快速上手.md` | `00-getting-started/quick-start.md` | 移动+合并 | 简化为单文件 |
| `getting-started/prerequisites/系统要求.md` | `00-getting-started/prerequisites.md` | 移动+合并 | 简化为单文件 |
| `getting-started/installation/安装_ColorVision.md` | `00-getting-started/installation.md` | 移动+合并 | 简化为单文件 |
| `introduction/what-is-colorvision/什么是_ColorVision_.md` | `00-getting-started/what-is-colorvision.md` | 移动 | 归入快速入门 |
| `introduction/key-features/主要特性.md` | 保留或移动 | 待定 | 可能保留在 introduction |

**新增文件**：
- [ ] 3.1.1 创建 `00-getting-started/first-steps.md`（首次运行指南）

**任务清单**：
- [ ] 3.1.2 移动并重命名入门指南为 README.md
- [ ] 3.1.3 合并快速上手相关文档
- [ ] 3.1.4 合并系统要求文档
- [ ] 3.1.5 合并安装指南文档
- [ ] 3.1.6 移动"什么是 ColorVision"
- [ ] 3.1.7 创建首次运行指南
- [ ] 3.1.8 更新所有内部链接

### 阶段四：迁移用户指南文档

#### 4.1 迁移界面使用文档

**源目录**: `user-interface-guide/`  
**目标目录**: `01-user-guide/interface/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `user-interface-guide/main-window/主窗口导览.md` | `01-user-guide/interface/main-window.md` | 移动+重命名 |
| `user-interface-guide/property-editor/属性编辑器.md` | `01-user-guide/interface/property-editor.md` | 移动（或归入开发指南） |

**新增文件**：
- [ ] 4.1.1 创建 `01-user-guide/interface/toolbar.md`
- [ ] 4.1.2 创建 `01-user-guide/interface/menu.md`
- [ ] 4.1.3 创建 `01-user-guide/interface/shortcuts.md`

**任务清单**：
- [ ] 4.1.4 移动主窗口导览文档
- [ ] 4.1.5 移动或复制属性编辑器文档
- [ ] 4.1.6 创建工具栏使用文档
- [ ] 4.1.7 创建菜单系统文档
- [ ] 4.1.8 创建快捷键文档
- [ ] 4.1.9 创建 README.md 作为界面使用总览

#### 4.2 迁移图像编辑器文档

**源目录**: `user-interface-guide/image-editor/`  
**目标目录**: `01-user-guide/image-editor/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `user-interface-guide/image-editor/图像编辑器.md` | `01-user-guide/image-editor/overview.md` | 移动+重命名 |

**新增文件**：
- [ ] 4.2.1 创建 `opening-images.md`（打开图像）
- [ ] 4.2.2 创建 `roi-tools.md`（ROI工具）
- [ ] 4.2.3 创建 `annotations.md`（标注功能）
- [ ] 4.2.4 创建 `export.md`（导出功能）

**任务清单**：
- [ ] 4.2.5 拆分图像编辑器文档为多个主题
- [ ] 4.2.6 创建各个子主题文档
- [ ] 4.2.7 更新链接和导航

#### 4.3 迁移设备使用文档

**源目录**: `device-management/`（用户相关部分）  
**目标目录**: `01-user-guide/devices/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `device-management/device-services-overview/设备服务概览.md` | `01-user-guide/devices/overview.md` | 移动（用户部分） |
| `device-management/adding-configuring-devices/添加与配置设备.md` | `01-user-guide/devices/adding-devices.md` | 移动+重命名 |
| `device-management/camera-service/相机服务.md` | `01-user-guide/devices/camera.md` | 提取用户部分 |

**任务清单**：
- [ ] 4.3.1 提取设备概览的用户相关内容
- [ ] 4.3.2 移动设备添加配置文档
- [ ] 4.3.3 提取相机使用的用户部分
- [ ] 4.3.4 创建校准设备使用文档
- [ ] 4.3.5 创建电机控制使用文档
- [ ] 4.3.6 创建其他设备使用文档

#### 4.4 迁移工作流程文档

**源目录**: 多个位置  
**目标目录**: `01-user-guide/workflow/`

**新增文件**（从现有文档提取或新建）：
- [ ] 4.4.1 创建 `flow-editor.md`（流程编辑器使用）
- [ ] 4.4.2 创建 `templates.md`（模板使用）
- [ ] 4.4.3 创建 `batch-process.md`（批量处理）
- [ ] 4.4.4 创建 `automation.md`（自动化）

#### 4.5 迁移数据管理文档

**源目录**: 多个位置  
**目标目录**: `01-user-guide/data-management/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `data-storage/README.md`（用户部分） | `01-user-guide/data-management/database.md` | 提取 |

**新增文件**：
- [ ] 4.5.1 创建 `solutions.md`（解决方案管理）
- [ ] 4.5.2 创建 `results.md`（结果查看）
- [ ] 4.5.3 创建 `export-import.md`（导入导出）

#### 4.6 迁移故障排查文档

**源目录**: `troubleshooting/`  
**目标目录**: `01-user-guide/troubleshooting/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `troubleshooting/故障排除.md` | `01-user-guide/troubleshooting/common-issues.md` | 移动+拆分 |

**新增文件**：
- [ ] 4.6.1 创建 `error-codes.md`（错误代码）
- [ ] 4.6.2 创建 `faq.md`（常见问答）

### 阶段五：迁移开发指南文档

#### 5.1 创建开发入门文档

**目标目录**: `02-developer-guide/getting-started/`

**新增文件**（从现有文档提取或新建）：
- [ ] 5.1.1 创建 `development-setup.md`（开发环境搭建）
- [ ] 5.1.2 创建 `build-from-source.md`（从源码构建）
- [ ] 5.1.3 创建 `project-structure.md`（链接到 project-structure）
- [ ] 5.1.4 创建 `coding-standards.md`（编码规范）

#### 5.2 迁移核心概念文档

**目标目录**: `02-developer-guide/core-concepts/`

**新增文件**（从现有文档提取或新建）：
- [ ] 5.2.1 创建 `mvvm-pattern.md`
- [ ] 5.2.2 创建 `dependency-injection.md`
- [ ] 5.2.3 创建 `configuration.md`（配置系统）
- [ ] 5.2.4 创建 `logging.md`（日志系统，从 user-interface-guide/log-viewer 提取开发者部分）
- [ ] 5.2.5 创建 `i18n.md`（国际化）

#### 5.3 迁移 UI 开发文档

**源目录**: `ui-components/`（开发者相关部分）  
**目标目录**: `02-developer-guide/ui-development/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `ui-components/ColorVision.Themes.md`（开发部分） | `02-developer-guide/ui-development/themes.md` | 提取 |
| `ui-components/HotKey系统设计文档.md` | `02-developer-guide/ui-development/hotkey-system.md` | 移动 |
| `user-interface-guide/property-editor/属性编辑器.md`（开发部分） | `02-developer-guide/ui-development/property-editor.md` | 提取 |

**新增文件**：
- [ ] 5.3.1 创建 `overview.md`（UI 开发概览）
- [ ] 5.3.2 创建 `controls.md`（自定义控件）
- [ ] 5.3.3 创建 `data-binding.md`（数据绑定）

**任务清单**：
- [ ] 5.3.4 提取主题开发文档
- [ ] 5.3.5 移动热键系统文档
- [ ] 5.3.6 提取属性编辑器开发文档
- [ ] 5.3.7 创建控件开发文档
- [ ] 5.3.8 创建数据绑定文档

#### 5.4 迁移 Engine 开发文档

**源目录**: `engine-components/`（开发部分）、`algorithm-engine-templates/`  
**目标目录**: `02-developer-guide/engine-development/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `engine-components/ColorVision.Engine.md`（开发部分） | `02-developer-guide/engine-development/overview.md` | 提取 |
| `algorithm-engine-templates/算法引擎与模板.md` | `02-developer-guide/engine-development/templates.md` | 移动+重组 |
| `algorithm-engine-templates/flow-engine/流程引擎.md` | `02-developer-guide/engine-development/flow-engine.md` | 移动 |
| `device-management/`（开发部分） | `02-developer-guide/engine-development/devices.md` | 提取 |

**新增文件**：
- [ ] 5.4.1 创建 `services.md`（服务开发）
- [ ] 5.4.2 创建 `algorithms.md`（算法集成）

**任务清单**：
- [ ] 5.4.3 提取 Engine 开发概览
- [ ] 5.4.4 重组模板开发文档
- [ ] 5.4.5 移动流程引擎开发文档
- [ ] 5.4.6 提取设备驱动开发文档
- [ ] 5.4.7 创建服务开发文档
- [ ] 5.4.8 创建算法集成文档

#### 5.5 迁移插件开发文档

**源目录**: `plugins/`、`extensibility/`  
**目标目录**: `02-developer-guide/plugin-development/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `plugins/plugin-management/插件管理.md` | `02-developer-guide/plugin-development/overview.md` | 提取开发部分 |
| `plugins/developing-a-plugin.md` | `02-developer-guide/plugin-development/getting-started.md` | 移动 |
| `plugins/plugin-lifecycle.md` | `02-developer-guide/plugin-development/lifecycle.md` | 移动 |
| `extensibility/README.md` | 合并到多个文件 | 拆分 |

**新增文件**：
- [ ] 5.5.1 创建 `plugin-types.md`（插件类型）
- [ ] 5.5.2 创建 `manifest.md`（清单文件）
- [ ] 5.5.3 创建 `debugging.md`（调试插件）
- [ ] 5.5.4 创建 `examples.md`（示例插件）

**任务清单**：
- [ ] 5.5.5 提取插件管理的开发部分
- [ ] 5.5.6 移动插件开发入门文档
- [ ] 5.5.7 移动插件生命周期文档
- [ ] 5.5.8 创建插件类型文档
- [ ] 5.5.9 创建清单文件文档
- [ ] 5.5.10 创建调试文档
- [ ] 5.5.11 创建示例文档

#### 5.6 迁移测试文档

**目标目录**: `02-developer-guide/testing/`

**新增文件**（全新创建）：
- [ ] 5.6.1 创建 `overview.md`
- [ ] 5.6.2 创建 `unit-testing.md`
- [ ] 5.6.3 创建 `integration-testing.md`
- [ ] 5.6.4 创建 `ui-testing.md`

#### 5.7 迁移性能优化文档

**源目录**: `performance/`  
**目标目录**: `02-developer-guide/performance/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `performance/README.md` | `02-developer-guide/performance/overview.md` | 移动+重命名 |

**新增文件**：
- [ ] 5.7.1 创建 `profiling.md`（性能分析）
- [ ] 5.7.2 创建 `optimization.md`（优化技巧）
- [ ] 5.7.3 创建 `best-practices.md`（最佳实践）

#### 5.8 迁移部署文档

**源目录**: `deployment/`、`update/`  
**目标目录**: `02-developer-guide/deployment/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `deployment/README.md` | `02-developer-guide/deployment/overview.md` | 移动+重命名 |
| `update/README.md` | `02-developer-guide/deployment/auto-update.md` | 移动+重命名 |

**新增文件**：
- [ ] 5.8.1 创建 `packaging.md`（打包发布）
- [ ] 5.8.2 创建 `installer.md`（安装程序）
- [ ] 5.8.3 创建 `licensing.md`（许可证）

### 阶段六：迁移架构文档

#### 6.1 迁移系统概览文档

**源目录**: `architecture/`、`introduction/`  
**目标目录**: `03-architecture/overview/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `architecture/README.md` | `03-architecture/README.md` | 移动 |
| `introduction/system-architecture/系统架构概览.md` | `03-architecture/overview/system-architecture.md` | 移动 |
| `architecture/architecture-runtime.md` | `03-architecture/overview/runtime.md` | 移动 |
| `architecture/component-interactions.md` | `03-architecture/overview/component-interactions.md` | 移动 |

**新增文件**：
- [ ] 6.1.1 创建 `design-principles.md`（设计原则）
- [ ] 6.1.2 创建 `technology-stack.md`（技术栈）
- [ ] 6.1.3 创建 `module-map.md`（模块映射，链接到 project-structure）

**任务清单**：
- [ ] 6.1.4 移动架构总览文档
- [ ] 6.1.5 移动系统架构概览
- [ ] 6.1.6 移动架构运行时文档
- [ ] 6.1.7 移动组件交互文档
- [ ] 6.1.8 创建设计原则文档
- [ ] 6.1.9 创建技术栈文档

#### 6.2 创建分层架构文档

**目标目录**: `03-architecture/layers/`

**新增文件**（全新创建）：
- [ ] 6.2.1 创建 `overview.md`
- [ ] 6.2.2 创建 `ui-layer.md`
- [ ] 6.2.3 创建 `engine-layer.md`
- [ ] 6.2.4 创建 `data-layer.md`
- [ ] 6.2.5 创建 `communication-layer.md`

#### 6.3 迁移核心组件文档

**源目录**: `architecture/`、`engine-components/`、`ui-components/`  
**目标目录**: `03-architecture/components/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `architecture/FlowEngineLib-Architecture.md` | `03-architecture/components/engine/flow-engine.md` | 移动 |
| `engine-components/ColorVision.Engine.md`（架构部分） | `03-architecture/components/engine/overview.md` | 提取 |
| `ui-components/UI组件概览.md`（架构部分） | `03-architecture/components/ui/overview.md` | 提取 |

**新增文件**：
- [ ] 6.3.1 创建 `colorvision-app.md`
- [ ] 6.3.2 创建组件架构各子目录的文档

**任务清单**：
- [ ] 6.3.3 移动 FlowEngineLib 架构文档
- [ ] 6.3.4 提取 Engine 组件架构部分
- [ ] 6.3.5 提取 UI 组件架构部分
- [ ] 6.3.6 创建各组件架构文档

#### 6.4 创建设计模式文档

**目标目录**: `03-architecture/patterns/`

**新增文件**（全新创建）：
- [ ] 6.4.1 创建 `mvvm.md`
- [ ] 6.4.2 创建 `dependency-injection.md`
- [ ] 6.4.3 创建 `event-aggregator.md`
- [ ] 6.4.4 创建 `command-pattern.md`
- [ ] 6.4.5 创建 `factory-pattern.md`

#### 6.5 创建数据流文档

**目标目录**: `03-architecture/data-flow/`

**新增文件**（全新创建）：
- [ ] 6.5.1 创建 `overview.md`
- [ ] 6.5.2 创建 `device-to-ui.md`
- [ ] 6.5.3 创建 `algorithm-results.md`
- [ ] 6.5.4 创建 `persistence.md`

#### 6.6 迁移安全文档

**源目录**: `security/`、`rbac/`  
**目标目录**: `03-architecture/security/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `security/README.md` | `03-architecture/security/overview.md` | 移动+重命名 |
| `rbac/rbac-model.md` | `03-architecture/security/rbac.md` | 移动 |

**新增文件**：
- [ ] 6.6.1 创建 `authentication.md`
- [ ] 6.6.2 创建 `authorization.md`

**任务清单**：
- [ ] 6.6.3 移动安全概览文档
- [ ] 6.6.4 移动 RBAC 文档
- [ ] 6.6.5 创建认证文档
- [ ] 6.6.6 创建授权文档

#### 6.7 迁移重构计划文档

**源目录**: `architecture/`（Engine 重构相关）  
**目标目录**: `03-architecture/refactoring/engine-refactoring/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `architecture/ColorVision.Engine-Refactoring-README.md` | `03-architecture/refactoring/engine-refactoring/overview.md` | 移动+重命名 |
| `architecture/ColorVision.Engine-Refactoring-Plan.md` | `03-architecture/refactoring/engine-refactoring/plan.md` | 移动 |
| `architecture/ColorVision.Engine-Refactoring-Summary.md` | `03-architecture/refactoring/engine-refactoring/summary.md` | 移动 |
| `architecture/ColorVision.Engine-Refactoring-Diagrams.md` | `03-architecture/refactoring/engine-refactoring/diagrams.md` | 移动 |
| `architecture/ColorVision.Engine-Refactoring-Checklist.md` | `03-architecture/refactoring/engine-refactoring/checklist.md` | 移动 |

**新增文件**：
- [ ] 6.7.1 创建 `future-plans.md`（未来计划）

**任务清单**：
- [ ] 6.7.2 移动所有 Engine 重构文档
- [ ] 6.7.3 创建未来计划文档

### 阶段七：迁移 API 参考文档

#### 7.1 迁移 UI 组件 API

**源目录**: `ui-components/`  
**目标目录**: `04-api-reference/ui-components/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `ui-components/ColorVision.UI.md` | `04-api-reference/ui-components/ColorVision.UI.md` | 移动 |
| `ui-components/ColorVision.Common.md` | `04-api-reference/ui-components/ColorVision.Common.md` | 移动 |
| `ui-components/ColorVision.Core.md` | `04-api-reference/ui-components/ColorVision.Core.md` | 移动 |
| `ui-components/ColorVision.Themes.md` | `04-api-reference/ui-components/ColorVision.Themes.md` | 移动 |
| `ui-components/ColorVision.ImageEditor.md` | `04-api-reference/ui-components/ColorVision.ImageEditor.md` | 移动 |
| `ui-components/ColorVision.Solution.md` | `04-api-reference/ui-components/ColorVision.Solution.md` | 移动 |
| `ui-components/ColorVision.Scheduler.md` | `04-api-reference/ui-components/ColorVision.Scheduler.md` | 移动 |
| `ui-components/ColorVision.Database.md` | `04-api-reference/ui-components/ColorVision.Database.md` | 移动 |
| `ui-components/ColorVision.SocketProtocol.md` | `04-api-reference/ui-components/ColorVision.SocketProtocol.md` | 移动 |

**删除文件**：
- [ ] 7.1.1 删除 `ui-components/HotKey-System-Design.md`（重复）
- [ ] 7.1.2 删除 `ui-components/ColorVision.Themes-优化说明.md`（非API文档）

**任务清单**：
- [ ] 7.1.3 移动所有 UI 组件 API 文档
- [ ] 7.1.4 删除重复和非API文档
- [ ] 7.1.5 统一文档格式
- [ ] 7.1.6 创建 README.md 总览

#### 7.2 迁移 Engine 组件 API

**源目录**: `engine-components/`  
**目标目录**: `04-api-reference/engine-components/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `engine-components/ColorVision.Engine.md` | `04-api-reference/engine-components/ColorVision.Engine.md` | 移动 |
| `engine-components/ColorVision.FileIO.md` | `04-api-reference/engine-components/ColorVision.FileIO.md` | 移动 |
| `engine-components/cvColorVision.md` | `04-api-reference/engine-components/cvColorVision.md` | 移动 |
| `engine-components/FlowEngineLib.md` | `04-api-reference/engine-components/FlowEngineLib.md` | 移动 |
| `engine-components/ST.Library.UI.md` | `04-api-reference/engine-components/ST.Library.UI.md` | 移动 |

**任务清单**：
- [ ] 7.2.1 移动所有 Engine 组件 API 文档
- [ ] 7.2.2 统一文档格式
- [ ] 7.2.3 创建 README.md 总览

#### 7.3 整合服务 API

**源目录**: `device-management/`（API 部分）  
**目标目录**: `04-api-reference/services/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `device-management/camera-service/相机服务.md`（API部分） | `04-api-reference/services/camera-service.md` | 提取+合并 |
| `camera-service/`（合并到上面） | 同上 | 合并 |
| `device-management/calibration-service/校准服务.md`（API部分） | `04-api-reference/services/calibration-service.md` | 提取 |
| `device-management/motor-service/电机服务.md`（API部分） | `04-api-reference/services/motor-service.md` | 提取 |
| `device-management/file-server-service/文件服务.md`（API部分） | `04-api-reference/services/file-service.md` | 提取 |
| `device-management/source-measure-unit-smu-service/`（API部分） | `04-api-reference/services/smu-service.md` | 提取 |

**新增文件**：
- [ ] 7.3.1 创建 `device-services.md`（设备服务总览）

**任务清单**：
- [ ] 7.3.2 提取并合并相机服务 API
- [ ] 7.3.3 提取校准服务 API
- [ ] 7.3.4 提取电机服务 API
- [ ] 7.3.5 提取文件服务 API
- [ ] 7.3.6 提取 SMU 服务 API
- [ ] 7.3.7 创建服务总览文档

#### 7.4 整合算法 API

**源目录**: `algorithms/`、`algorithm-engine-templates/`、`common-algorithm-primitives/`  
**目标目录**: `04-api-reference/algorithms/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `algorithms/overview.md` | `04-api-reference/algorithms/overview.md` | 移动 |
| `algorithms/ghost-detection.md` | `04-api-reference/algorithms/detectors/ghost-detection.md` | 移动 |
| `algorithm-engine-templates/templates-architecture/POI模板详解.md` | `04-api-reference/algorithms/templates/poi-template.md` | 移动 |
| `algorithm-engine-templates/templates-architecture/ARVR模板详解.md` | `04-api-reference/algorithms/templates/arvr-template.md` | 移动 |
| `common-algorithm-primitives/roi-region-of-interest/ROI_(感兴趣区域).md` | `04-api-reference/algorithms/primitives/roi.md` | 移动 |
| `common-algorithm-primitives/poi-point-of-interest/POI_(关注点).md` | `04-api-reference/algorithms/primitives/poi.md` | 移动 |

**新增文件**：
- [ ] 7.4.1 创建 `templates/template-base.md`
- [ ] 7.4.2 创建 `templates/custom-template.md`
- [ ] 7.4.3 创建 `detectors/pattern-detection.md`

**任务清单**：
- [ ] 7.4.4 移动算法概览
- [ ] 7.4.5 移动检测算法文档
- [ ] 7.4.6 移动模板 API 文档
- [ ] 7.4.7 移动算法原语文档
- [ ] 7.4.8 创建新的 API 文档

#### 7.5 整合插件 API

**源目录**: `plugins/`（API 部分）  
**目标目录**: `04-api-reference/plugins/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `plugins/using-standard-plugins/pattern.md` | `04-api-reference/plugins/standard-plugins/pattern.md` | 移动 |
| `plugins/system-monitor.md` | `04-api-reference/plugins/standard-plugins/system-monitor.md` | 移动 |

**新增文件**：
- [ ] 7.5.1 创建 `plugin-interface.md`
- [ ] 7.5.2 创建 `plugin-base.md`
- [ ] 7.5.3 创建 `standard-plugins/event-viewer.md`
- [ ] 7.5.4 创建 `standard-plugins/screen-recorder.md`

**任务清单**：
- [ ] 7.5.5 移动标准插件 API 文档
- [ ] 7.5.6 创建插件接口文档
- [ ] 7.5.7 创建插件基类文档
- [ ] 7.5.8 创建标准插件 API 文档

#### 7.6 整合扩展点 API

**源目录**: `extensibility/`（API 部分）  
**目标目录**: `04-api-reference/extensions/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `extensibility/FlowEngineLib-NodeDevelopment.md`（API部分） | `04-api-reference/extensions/flow-node.md` | 提取 |

**新增文件**：
- [ ] 7.6.1 创建 `property-editor.md`
- [ ] 7.6.2 创建 `result-handler.md`
- [ ] 7.6.3 创建 `drawing-visual.md`
- [ ] 7.6.4 创建 `config-provider.md`

**任务清单**：
- [ ] 7.6.5 提取流程节点 API
- [ ] 7.6.6 创建属性编辑器扩展 API
- [ ] 7.6.7 创建结果处理器 API
- [ ] 7.6.8 创建绘图可视化 API
- [ ] 7.6.9 创建配置提供者 API

### 阶段八：整理资源文档

#### 8.1 保留项目结构文档

**源目录**: `project-structure/`  
**目标目录**: `05-resources/project-structure/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `project-structure/README.md` | `05-resources/project-structure/README.md` | 移动 |
| `project-structure/module-documentation-map.md` | `05-resources/project-structure/module-documentation-map.md` | 移动 |

**任务清单**：
- [ ] 8.1.1 移动项目结构文档
- [ ] 8.1.2 更新文档内的链接

#### 8.2 整理更新日志

**源目录**: `update/`  
**目标目录**: `05-resources/changelog/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| GitHub CHANGELOG.md | `05-resources/changelog/README.md` | 链接 |
| `update/changelog-window.md` | `05-resources/changelog/window.md` | 移动 |
| `update/changelog-window-comparison.md` | 删除或归档 | 非用户文档 |

**任务清单**：
- [ ] 8.2.1 创建 changelog README（链接到 GitHub CHANGELOG）
- [ ] 8.2.2 移动更新日志窗口文档
- [ ] 8.2.3 决定比较文档的去留

#### 8.3 创建术语表

**目标目录**: `05-resources/glossary/`

**新增文件**：
- [ ] 8.3.1 创建 `README.md`（术语定义）

#### 8.4 整理文档模板

**源目录**: `_templates/`  
**目标目录**: `05-resources/templates/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `_templates/doc-template.md` | `05-resources/templates/doc-template.md` | 移动 |

**新增文件**：
- [ ] 8.4.1 创建 `api-template.md`
- [ ] 8.4.2 创建 `tutorial-template.md`

**任务清单**：
- [ ] 8.4.3 移动现有模板
- [ ] 8.4.4 创建新模板

#### 8.5 整理法律文档

**源目录**: 根目录散乱文件  
**目标目录**: `05-resources/legal/`

| 源文件 | 目标文件 | 操作 |
|-------|---------|-----|
| `ColorVision API V1.1.md` | `05-resources/legal/api-v1.1.md` | 移动+重命名 |
| `Software License Agreement.md` | `05-resources/legal/software-agreement.md` | 移动+重命名 |

**新增文件**：
- [ ] 8.5.1 创建 `license.md`（MIT License）

**任务清单**：
- [ ] 8.5.2 移动 API 文档
- [ ] 8.5.3 移动许可协议
- [ ] 8.5.4 创建许可证文档

## 📝 下一步

继续查看**下篇**，包含：
- VitePress 配置详细更新
- 质量检查清单
- 测试验证步骤
- 维护指南

---

**文档版本**: v1.0  
**创建日期**: 2025-11-03  
**状态**: 待执行

所有任务完成并勾选后，由用户确认删除此计划文档。
