# VitePress 文档结构重组计划（上篇）

## 📋 项目概述

### 当前问题分析

通过对 docs 目录的全面扫描，发现以下问题：

1. **结构重复**：
   - `camera-service/` 和 `device-management/camera-service/` 重复
   - `common-algorithm-primitives/` 在两个位置（顶层和 algorithm-engine-templates 下）
   - `api-reference/` 在两个位置（顶层和 developer-guide 下）

2. **分类混乱**：
   - 100 个 markdown 文件分散在 60+ 个目录中
   - 部分文档命名不一致（中英文混杂，如 HotKey-System-Design.md 和 HotKey系统设计文档.md）
   - 根目录存在散乱文件（ColorVision API V1.1.md, Software License Agreement.md 等）

3. **层级不清**：
   - flow-engine 既在顶层又在 algorithm-engine-templates 下
   - 用户文档、开发者文档、架构文档混在一起

4. **缺失内容**：
   - 部分目录只有 README.md，缺少详细文档
   - 缺少统一的文档模板和规范

### 重组目标

1. **统一文档体验**：所有文档使用中文，遵循统一规范
2. **清晰的层次结构**：用户文档、开发者文档、参考文档分离
3. **消除重复**：合并重复内容，建立唯一权威来源
4. **便于维护**：文档与代码模块对应，易于更新
5. **易于导航**：从用户视角和开发者视角都能快速找到所需文档

### 重组原则

1. **以用户为中心**：按使用场景组织，而非技术实现
2. **渐进式学习**：从入门到深入，层次分明
3. **单一信息源**：每个主题只在一个地方详细说明
4. **模块对应**：文档结构尽量映射代码结构
5. **持续维护**：预留扩展空间，便于增量更新

## 📂 新文档结构设计

### 一级分类（7 大类）

```
docs/
├── 00-getting-started/          # 快速入门（新用户）
├── 01-user-guide/               # 用户指南（使用者）
├── 02-developer-guide/          # 开发指南（开发者）
├── 03-architecture/             # 架构设计（架构师）
├── 04-api-reference/            # API 参考（开发者）
├── 05-resources/                # 资源文档（所有人）
└── .vitepress/                  # VitePress 配置
```

### 详细目录结构

```
docs/
│
├── index.md                     # 首页
├── .vitepress/                  # VitePress 配置
│   ├── config.mts              # 主配置文件
│   └── theme/                  # 主题定制
│
├── 00-getting-started/          # 📚 快速入门
│   ├── README.md               # 入门总览
│   ├── what-is-colorvision.md  # 什么是 ColorVision
│   ├── quick-start.md          # 快速开始（5分钟上手）
│   ├── installation.md         # 安装指南
│   ├── prerequisites.md        # 系统要求
│   └── first-steps.md          # 第一步（首次运行）
│
├── 01-user-guide/               # 📖 用户指南
│   ├── README.md               # 用户指南总览
│   │
│   ├── interface/              # 界面使用
│   │   ├── main-window.md      # 主窗口导览
│   │   ├── toolbar.md          # 工具栏
│   │   ├── menu.md             # 菜单系统
│   │   └── shortcuts.md        # 快捷键
│   │
│   ├── image-editor/           # 图像编辑器
│   │   ├── overview.md         # 编辑器概览
│   │   ├── opening-images.md   # 打开图像
│   │   ├── roi-tools.md        # ROI 工具
│   │   ├── annotations.md      # 标注功能
│   │   └── export.md           # 导出功能
│   │
│   ├── devices/                # 设备使用
│   │   ├── overview.md         # 设备概览
│   │   ├── camera.md           # 相机使用
│   │   ├── calibration.md      # 校准设备
│   │   ├── motor.md            # 电机控制
│   │   └── other-devices.md    # 其他设备
│   │
│   ├── workflow/               # 工作流程
│   │   ├── flow-editor.md      # 流程编辑器
│   │   ├── templates.md        # 模板使用
│   │   ├── batch-process.md    # 批量处理
│   │   └── automation.md       # 自动化
│   │
│   ├── data-management/        # 数据管理
│   │   ├── solutions.md        # 解决方案管理
│   │   ├── results.md          # 结果查看
│   │   ├── database.md         # 数据库
│   │   └── export-import.md    # 导入导出
│   │
│   └── troubleshooting/        # 故障排查
│       ├── common-issues.md    # 常见问题
│       ├── error-codes.md      # 错误代码
│       └── faq.md              # 常见问答
│
├── 02-developer-guide/          # 👨‍💻 开发指南
│   ├── README.md               # 开发指南总览
│   │
│   ├── getting-started/        # 开发入门
│   │   ├── development-setup.md    # 开发环境搭建
│   │   ├── build-from-source.md    # 从源码构建
│   │   ├── project-structure.md    # 项目结构（链接到 project-structure/）
│   │   └── coding-standards.md     # 编码规范
│   │
│   ├── core-concepts/          # 核心概念
│   │   ├── mvvm-pattern.md     # MVVM 模式
│   │   ├── dependency-injection.md # 依赖注入
│   │   ├── configuration.md    # 配置系统
│   │   ├── logging.md          # 日志系统
│   │   └── i18n.md             # 国际化
│   │
│   ├── ui-development/         # UI 开发
│   │   ├── overview.md         # UI 开发概览
│   │   ├── themes.md           # 主题开发
│   │   ├── controls.md         # 自定义控件
│   │   ├── property-editor.md  # 属性编辑器
│   │   ├── data-binding.md     # 数据绑定
│   │   └── hotkey-system.md    # 热键系统
│   │
│   ├── engine-development/     # Engine 开发
│   │   ├── overview.md         # Engine 概览
│   │   ├── services.md         # 服务开发
│   │   ├── devices.md          # 设备驱动
│   │   ├── algorithms.md       # 算法集成
│   │   ├── templates.md        # 模板开发
│   │   └── flow-engine.md      # 流程引擎
│   │
│   ├── plugin-development/     # 插件开发
│   │   ├── overview.md         # 插件概览
│   │   ├── getting-started.md  # 开发入门
│   │   ├── plugin-types.md     # 插件类型
│   │   ├── lifecycle.md        # 生命周期
│   │   ├── manifest.md         # 清单文件
│   │   ├── debugging.md        # 调试插件
│   │   └── examples.md         # 示例插件
│   │
│   ├── testing/                # 测试
│   │   ├── overview.md         # 测试概览
│   │   ├── unit-testing.md     # 单元测试
│   │   ├── integration-testing.md # 集成测试
│   │   └── ui-testing.md       # UI 测试
│   │
│   ├── performance/            # 性能优化
│   │   ├── overview.md         # 性能概览
│   │   ├── profiling.md        # 性能分析
│   │   ├── optimization.md     # 优化技巧
│   │   └── best-practices.md   # 最佳实践
│   │
│   └── deployment/             # 部署
│       ├── overview.md         # 部署概览
│       ├── packaging.md        # 打包发布
│       ├── installer.md        # 安装程序
│       ├── auto-update.md      # 自动更新
│       └── licensing.md        # 许可证
│
├── 03-architecture/             # 🏗️ 架构设计
│   ├── README.md               # 架构总览
│   │
│   ├── overview/               # 系统概览
│   │   ├── system-architecture.md  # 系统架构
│   │   ├── design-principles.md    # 设计原则
│   │   ├── technology-stack.md     # 技术栈
│   │   └── module-map.md           # 模块映射（链接到 project-structure/）
│   │
│   ├── layers/                 # 分层架构
│   │   ├── overview.md         # 分层概览
│   │   ├── ui-layer.md         # UI 层
│   │   ├── engine-layer.md     # Engine 层
│   │   ├── data-layer.md       # 数据层
│   │   └── communication-layer.md # 通信层
│   │
│   ├── components/             # 核心组件
│   │   ├── colorvision-app.md  # ColorVision 主程序
│   │   ├── engine/             # Engine 组件
│   │   │   ├── overview.md     # Engine 概览
│   │   │   ├── services.md     # 服务架构
│   │   │   ├── templates.md    # 模板系统
│   │   │   ├── flow-engine.md  # 流程引擎
│   │   │   └── mqtt.md         # MQTT 通信
│   │   ├── ui/                 # UI 组件
│   │   │   ├── overview.md     # UI 概览
│   │   │   ├── framework.md    # UI 框架
│   │   │   ├── themes.md       # 主题系统
│   │   │   ├── image-editor.md # 图像编辑器
│   │   │   └── scheduler.md    # 调度器
│   │   └── plugins/            # 插件系统
│   │       ├── architecture.md # 插件架构
│   │       ├── discovery.md    # 插件发现
│   │       └── loading.md      # 插件加载
│   │
│   ├── patterns/               # 设计模式
│   │   ├── mvvm.md             # MVVM 模式
│   │   ├── dependency-injection.md # 依赖注入
│   │   ├── event-aggregator.md # 事件聚合
│   │   ├── command-pattern.md  # 命令模式
│   │   └── factory-pattern.md  # 工厂模式
│   │
│   ├── data-flow/              # 数据流
│   │   ├── overview.md         # 数据流概览
│   │   ├── device-to-ui.md     # 设备到 UI
│   │   ├── algorithm-results.md # 算法结果
│   │   └── persistence.md      # 数据持久化
│   │
│   ├── security/               # 安全设计
│   │   ├── overview.md         # 安全概览
│   │   ├── authentication.md   # 认证
│   │   ├── authorization.md    # 授权
│   │   └── rbac.md             # 基于角色的访问控制
│   │
│   └── refactoring/            # 重构计划
│       ├── engine-refactoring/ # Engine 重构
│       │   ├── overview.md     # 重构概览
│       │   ├── plan.md         # 完整计划
│       │   ├── summary.md      # 执行摘要
│       │   ├── diagrams.md     # 架构图表
│       │   └── checklist.md    # 检查清单
│       └── future-plans.md     # 未来计划
│
├── 04-api-reference/            # 📚 API 参考
│   ├── README.md               # API 参考总览
│   │
│   ├── ui-components/          # UI 组件 API
│   │   ├── ColorVision.UI.md   # ColorVision.UI
│   │   ├── ColorVision.Common.md # ColorVision.Common
│   │   ├── ColorVision.Core.md # ColorVision.Core
│   │   ├── ColorVision.Themes.md # ColorVision.Themes
│   │   ├── ColorVision.ImageEditor.md # 图像编辑器
│   │   ├── ColorVision.Solution.md # 解决方案
│   │   ├── ColorVision.Scheduler.md # 调度器
│   │   ├── ColorVision.Database.md # 数据库
│   │   └── ColorVision.SocketProtocol.md # Socket 协议
│   │
│   ├── engine-components/      # Engine 组件 API
│   │   ├── ColorVision.Engine.md # ColorVision.Engine
│   │   ├── ColorVision.FileIO.md # 文件 IO
│   │   ├── cvColorVision.md    # 视觉处理
│   │   ├── FlowEngineLib.md    # 流程引擎库
│   │   └── ST.Library.UI.md    # UI 库
│   │
│   ├── services/               # 服务 API
│   │   ├── device-services.md  # 设备服务
│   │   ├── camera-service.md   # 相机服务
│   │   ├── calibration-service.md # 校准服务
│   │   ├── motor-service.md    # 电机服务
│   │   ├── file-service.md     # 文件服务
│   │   └── smu-service.md      # SMU 服务
│   │
│   ├── algorithms/             # 算法 API
│   │   ├── overview.md         # 算法概览
│   │   ├── templates/          # 模板 API
│   │   │   ├── template-base.md # 模板基类
│   │   │   ├── poi-template.md # POI 模板
│   │   │   ├── arvr-template.md # ARVR 模板
│   │   │   └── custom-template.md # 自定义模板
│   │   ├── primitives/         # 算法原语
│   │   │   ├── roi.md          # ROI（感兴趣区域）
│   │   │   └── poi.md          # POI（关注点）
│   │   └── detectors/          # 检测算法
│   │       ├── ghost-detection.md # Ghost 检测
│   │       └── pattern-detection.md # 图案检测
│   │
│   ├── plugins/                # 插件 API
│   │   ├── plugin-interface.md # 插件接口
│   │   ├── plugin-base.md      # 插件基类
│   │   └── standard-plugins/   # 标准插件
│   │       ├── pattern.md      # Pattern 插件
│   │       ├── system-monitor.md # 系统监控
│   │       ├── event-viewer.md # 事件查看器
│   │       └── screen-recorder.md # 屏幕录制
│   │
│   └── extensions/             # 扩展点 API
│       ├── property-editor.md  # 属性编辑器扩展
│       ├── result-handler.md   # 结果处理器
│       ├── drawing-visual.md   # 绘图可视化
│       └── config-provider.md  # 配置提供者
│
├── 05-resources/                # 📦 资源文档
│   ├── README.md               # 资源总览
│   │
│   ├── project-structure/      # 项目结构（保留现有）
│   │   ├── README.md           # 结构总览
│   │   └── module-documentation-map.md # 模块文档对照
│   │
│   ├── changelog/              # 更新日志
│   │   ├── README.md           # 更新日志
│   │   └── migration-guides/   # 迁移指南
│   │
│   ├── glossary/               # 术语表
│   │   └── README.md           # 术语定义
│   │
│   ├── templates/              # 文档模板
│   │   ├── doc-template.md     # 通用文档模板
│   │   ├── api-template.md     # API 文档模板
│   │   └── tutorial-template.md # 教程模板
│   │
│   ├── assets/                 # 静态资源
│   │   ├── images/             # 图片
│   │   ├── diagrams/           # 图表
│   │   └── downloads/          # 下载文件
│   │
│   └── legal/                  # 法律文档
│       ├── license.md          # 许可证
│       └── software-agreement.md # 软件许可协议
│
└── public/                      # 公共资源（VitePress 静态文件）
    └── images/                 # 公共图片
```

## 🔄 迁移映射表

### 需要合并的重复内容

| 当前位置 | 目标位置 | 操作 |
|---------|---------|------|
| `camera-service/` | `04-api-reference/services/camera-service.md` | 合并 |
| `device-management/camera-service/` | 同上 | 合并 |
| `common-algorithm-primitives/` (顶层) | `04-api-reference/algorithms/primitives/` | 移动 |
| `algorithm-engine-templates/common-algorithm-primitives/` | 同上 | 合并 |
| `api-reference/` (顶层) | `04-api-reference/` | 移动 |
| `developer-guide/api-reference/` | 同上 | 合并 |
| `flow-engine/` (顶层) | `02-developer-guide/engine-development/flow-engine.md` | 移动 |
| `algorithm-engine-templates/flow-engine/` | 同上 | 合并 |

### 需要重新分类的文档

| 当前位置 | 目标位置 | 说明 |
|---------|---------|------|
| `getting-started/入门指南.md` | `00-getting-started/README.md` | 重命名 |
| `getting-started/quick-start/` | `00-getting-started/quick-start.md` | 合并为单文件 |
| `getting-started/installation/` | `00-getting-started/installation.md` | 合并为单文件 |
| `getting-started/prerequisites/` | `00-getting-started/prerequisites.md` | 合并为单文件 |
| `user-interface-guide/` | `01-user-guide/interface/` | 移动+重组 |
| `plugins/` | 拆分到多个位置 | 用户指南、开发指南、API 参考 |
| `ui-components/` | `04-api-reference/ui-components/` | 移动 |
| `engine-components/` | `04-api-reference/engine-components/` | 移动 |
| `device-management/` | 拆分 | 用户指南 + API 参考 |
| `algorithm-engine-templates/` | 拆分 | 开发指南 + API 参考 |

### 需要删除或归档的文档

| 文件/目录 | 操作 | 原因 |
|----------|------|------|
| `ColorVision API V1.1.md` | 移动到 `05-resources/legal/` | 根目录整理 |
| `Software License Agreement.md` | 移动到 `05-resources/legal/` | 根目录整理 |
| `_404.md` | 保留 | VitePress 配置文件 |
| `_templates/` | 移动到 `05-resources/templates/` | 重新组织 |
| 重复的 README.md | 合并 | 消除冗余 |

## 📝 下一步工作

本计划分为上、中、下三篇：

- **上篇（本文档）**：问题分析、目标设定、新结构设计
- **中篇**：详细迁移步骤、文件操作清单、VitePress 配置更新
- **下篇**：质量检查、测试验证、维护指南

---

**文档版本**: v1.0  
**创建日期**: 2025-11-03  
**状态**: 待审核

所有任务完成并勾选后，由用户确认删除此计划文档。
