# VitePress 文档结构重组计划（下篇）

## 📋 VitePress 配置更新

### 阶段九：更新 VitePress 配置

#### 9.1 更新侧边栏配置

**文件**: `docs/.vitepress/config.mts`

**新侧边栏结构**：

```typescript
sidebar: [
  // ========================================
  // 📚 快速入门
  // ========================================
  {
    text: '📚 快速入门',
    collapsed: false,
    items: [
      { text: '入门总览', link: '/00-getting-started/README' },
      { text: '什么是 ColorVision', link: '/00-getting-started/what-is-colorvision' },
      { text: '快速开始', link: '/00-getting-started/quick-start' },
      { text: '系统要求', link: '/00-getting-started/prerequisites' },
      { text: '安装指南', link: '/00-getting-started/installation' },
      { text: '首次运行', link: '/00-getting-started/first-steps' }
    ]
  },

  // ========================================
  // 📖 用户指南
  // ========================================
  {
    text: '📖 用户指南',
    collapsed: true,
    items: [
      { text: '用户指南总览', link: '/01-user-guide/README' },
      
      // 界面使用
      {
        text: '界面使用',
        collapsed: true,
        items: [
          { text: '主窗口导览', link: '/01-user-guide/interface/main-window' },
          { text: '工具栏', link: '/01-user-guide/interface/toolbar' },
          { text: '菜单系统', link: '/01-user-guide/interface/menu' },
          { text: '快捷键', link: '/01-user-guide/interface/shortcuts' }
        ]
      },
      
      // 图像编辑器
      {
        text: '图像编辑器',
        collapsed: true,
        items: [
          { text: '编辑器概览', link: '/01-user-guide/image-editor/overview' },
          { text: '打开图像', link: '/01-user-guide/image-editor/opening-images' },
          { text: 'ROI 工具', link: '/01-user-guide/image-editor/roi-tools' },
          { text: '标注功能', link: '/01-user-guide/image-editor/annotations' },
          { text: '导出功能', link: '/01-user-guide/image-editor/export' }
        ]
      },
      
      // 设备使用
      {
        text: '设备使用',
        collapsed: true,
        items: [
          { text: '设备概览', link: '/01-user-guide/devices/overview' },
          { text: '相机使用', link: '/01-user-guide/devices/camera' },
          { text: '校准设备', link: '/01-user-guide/devices/calibration' },
          { text: '电机控制', link: '/01-user-guide/devices/motor' },
          { text: '其他设备', link: '/01-user-guide/devices/other-devices' }
        ]
      },
      
      // 工作流程
      {
        text: '工作流程',
        collapsed: true,
        items: [
          { text: '流程编辑器', link: '/01-user-guide/workflow/flow-editor' },
          { text: '模板使用', link: '/01-user-guide/workflow/templates' },
          { text: '批量处理', link: '/01-user-guide/workflow/batch-process' },
          { text: '自动化', link: '/01-user-guide/workflow/automation' }
        ]
      },
      
      // 数据管理
      {
        text: '数据管理',
        collapsed: true,
        items: [
          { text: '解决方案管理', link: '/01-user-guide/data-management/solutions' },
          { text: '结果查看', link: '/01-user-guide/data-management/results' },
          { text: '数据库', link: '/01-user-guide/data-management/database' },
          { text: '导入导出', link: '/01-user-guide/data-management/export-import' }
        ]
      },
      
      // 故障排查
      {
        text: '故障排查',
        collapsed: true,
        items: [
          { text: '常见问题', link: '/01-user-guide/troubleshooting/common-issues' },
          { text: '错误代码', link: '/01-user-guide/troubleshooting/error-codes' },
          { text: '常见问答', link: '/01-user-guide/troubleshooting/faq' }
        ]
      }
    ]
  },

  // ========================================
  // 👨‍💻 开发指南
  // ========================================
  {
    text: '👨‍💻 开发指南',
    collapsed: true,
    items: [
      { text: '开发指南总览', link: '/02-developer-guide/README' },
      
      // 开发入门
      {
        text: '开发入门',
        collapsed: true,
        items: [
          { text: '开发环境搭建', link: '/02-developer-guide/getting-started/development-setup' },
          { text: '从源码构建', link: '/02-developer-guide/getting-started/build-from-source' },
          { text: '项目结构', link: '/02-developer-guide/getting-started/project-structure' },
          { text: '编码规范', link: '/02-developer-guide/getting-started/coding-standards' }
        ]
      },
      
      // 核心概念
      {
        text: '核心概念',
        collapsed: true,
        items: [
          { text: 'MVVM 模式', link: '/02-developer-guide/core-concepts/mvvm-pattern' },
          { text: '依赖注入', link: '/02-developer-guide/core-concepts/dependency-injection' },
          { text: '配置系统', link: '/02-developer-guide/core-concepts/configuration' },
          { text: '日志系统', link: '/02-developer-guide/core-concepts/logging' },
          { text: '国际化', link: '/02-developer-guide/core-concepts/i18n' }
        ]
      },
      
      // UI 开发
      {
        text: 'UI 开发',
        collapsed: true,
        items: [
          { text: 'UI 开发概览', link: '/02-developer-guide/ui-development/overview' },
          { text: '主题开发', link: '/02-developer-guide/ui-development/themes' },
          { text: '自定义控件', link: '/02-developer-guide/ui-development/controls' },
          { text: '属性编辑器', link: '/02-developer-guide/ui-development/property-editor' },
          { text: '数据绑定', link: '/02-developer-guide/ui-development/data-binding' },
          { text: '热键系统', link: '/02-developer-guide/ui-development/hotkey-system' }
        ]
      },
      
      // Engine 开发
      {
        text: 'Engine 开发',
        collapsed: true,
        items: [
          { text: 'Engine 概览', link: '/02-developer-guide/engine-development/overview' },
          { text: '服务开发', link: '/02-developer-guide/engine-development/services' },
          { text: '设备驱动', link: '/02-developer-guide/engine-development/devices' },
          { text: '算法集成', link: '/02-developer-guide/engine-development/algorithms' },
          { text: '模板开发', link: '/02-developer-guide/engine-development/templates' },
          { text: '流程引擎', link: '/02-developer-guide/engine-development/flow-engine' }
        ]
      },
      
      // 插件开发
      {
        text: '插件开发',
        collapsed: true,
        items: [
          { text: '插件概览', link: '/02-developer-guide/plugin-development/overview' },
          { text: '开发入门', link: '/02-developer-guide/plugin-development/getting-started' },
          { text: '插件类型', link: '/02-developer-guide/plugin-development/plugin-types' },
          { text: '生命周期', link: '/02-developer-guide/plugin-development/lifecycle' },
          { text: '清单文件', link: '/02-developer-guide/plugin-development/manifest' },
          { text: '调试插件', link: '/02-developer-guide/plugin-development/debugging' },
          { text: '示例插件', link: '/02-developer-guide/plugin-development/examples' }
        ]
      },
      
      // 测试
      {
        text: '测试',
        collapsed: true,
        items: [
          { text: '测试概览', link: '/02-developer-guide/testing/overview' },
          { text: '单元测试', link: '/02-developer-guide/testing/unit-testing' },
          { text: '集成测试', link: '/02-developer-guide/testing/integration-testing' },
          { text: 'UI 测试', link: '/02-developer-guide/testing/ui-testing' }
        ]
      },
      
      // 性能优化
      {
        text: '性能优化',
        collapsed: true,
        items: [
          { text: '性能概览', link: '/02-developer-guide/performance/overview' },
          { text: '性能分析', link: '/02-developer-guide/performance/profiling' },
          { text: '优化技巧', link: '/02-developer-guide/performance/optimization' },
          { text: '最佳实践', link: '/02-developer-guide/performance/best-practices' }
        ]
      },
      
      // 部署
      {
        text: '部署',
        collapsed: true,
        items: [
          { text: '部署概览', link: '/02-developer-guide/deployment/overview' },
          { text: '打包发布', link: '/02-developer-guide/deployment/packaging' },
          { text: '安装程序', link: '/02-developer-guide/deployment/installer' },
          { text: '自动更新', link: '/02-developer-guide/deployment/auto-update' },
          { text: '许可证', link: '/02-developer-guide/deployment/licensing' }
        ]
      }
    ]
  },

  // ========================================
  // 🏗️ 架构设计
  // ========================================
  {
    text: '🏗️ 架构设计',
    collapsed: true,
    items: [
      { text: '架构总览', link: '/03-architecture/README' },
      
      // 系统概览
      {
        text: '系统概览',
        collapsed: true,
        items: [
          { text: '系统架构', link: '/03-architecture/overview/system-architecture' },
          { text: '设计原则', link: '/03-architecture/overview/design-principles' },
          { text: '技术栈', link: '/03-architecture/overview/technology-stack' },
          { text: '模块映射', link: '/03-architecture/overview/module-map' }
        ]
      },
      
      // 分层架构
      {
        text: '分层架构',
        collapsed: true,
        items: [
          { text: '分层概览', link: '/03-architecture/layers/overview' },
          { text: 'UI 层', link: '/03-architecture/layers/ui-layer' },
          { text: 'Engine 层', link: '/03-architecture/layers/engine-layer' },
          { text: '数据层', link: '/03-architecture/layers/data-layer' },
          { text: '通信层', link: '/03-architecture/layers/communication-layer' }
        ]
      },
      
      // 核心组件
      {
        text: '核心组件',
        collapsed: true,
        items: [
          { text: 'ColorVision 主程序', link: '/03-architecture/components/colorvision-app' },
          {
            text: 'Engine 组件',
            collapsed: true,
            items: [
              { text: 'Engine 概览', link: '/03-architecture/components/engine/overview' },
              { text: '服务架构', link: '/03-architecture/components/engine/services' },
              { text: '模板系统', link: '/03-architecture/components/engine/templates' },
              { text: '流程引擎', link: '/03-architecture/components/engine/flow-engine' },
              { text: 'MQTT 通信', link: '/03-architecture/components/engine/mqtt' }
            ]
          },
          {
            text: 'UI 组件',
            collapsed: true,
            items: [
              { text: 'UI 概览', link: '/03-architecture/components/ui/overview' },
              { text: 'UI 框架', link: '/03-architecture/components/ui/framework' },
              { text: '主题系统', link: '/03-architecture/components/ui/themes' },
              { text: '图像编辑器', link: '/03-architecture/components/ui/image-editor' },
              { text: '调度器', link: '/03-architecture/components/ui/scheduler' }
            ]
          },
          {
            text: '插件系统',
            collapsed: true,
            items: [
              { text: '插件架构', link: '/03-architecture/components/plugins/architecture' },
              { text: '插件发现', link: '/03-architecture/components/plugins/discovery' },
              { text: '插件加载', link: '/03-architecture/components/plugins/loading' }
            ]
          }
        ]
      },
      
      // 设计模式
      {
        text: '设计模式',
        collapsed: true,
        items: [
          { text: 'MVVM 模式', link: '/03-architecture/patterns/mvvm' },
          { text: '依赖注入', link: '/03-architecture/patterns/dependency-injection' },
          { text: '事件聚合', link: '/03-architecture/patterns/event-aggregator' },
          { text: '命令模式', link: '/03-architecture/patterns/command-pattern' },
          { text: '工厂模式', link: '/03-architecture/patterns/factory-pattern' }
        ]
      },
      
      // 数据流
      {
        text: '数据流',
        collapsed: true,
        items: [
          { text: '数据流概览', link: '/03-architecture/data-flow/overview' },
          { text: '设备到 UI', link: '/03-architecture/data-flow/device-to-ui' },
          { text: '算法结果', link: '/03-architecture/data-flow/algorithm-results' },
          { text: '数据持久化', link: '/03-architecture/data-flow/persistence' }
        ]
      },
      
      // 安全设计
      {
        text: '安全设计',
        collapsed: true,
        items: [
          { text: '安全概览', link: '/03-architecture/security/overview' },
          { text: '认证', link: '/03-architecture/security/authentication' },
          { text: '授权', link: '/03-architecture/security/authorization' },
          { text: 'RBAC', link: '/03-architecture/security/rbac' }
        ]
      },
      
      // 重构计划
      {
        text: '重构计划',
        collapsed: true,
        items: [
          {
            text: 'Engine 重构',
            collapsed: true,
            items: [
              { text: '重构概览', link: '/03-architecture/refactoring/engine-refactoring/overview' },
              { text: '完整计划', link: '/03-architecture/refactoring/engine-refactoring/plan' },
              { text: '执行摘要', link: '/03-architecture/refactoring/engine-refactoring/summary' },
              { text: '架构图表', link: '/03-architecture/refactoring/engine-refactoring/diagrams' },
              { text: '检查清单', link: '/03-architecture/refactoring/engine-refactoring/checklist' }
            ]
          },
          { text: '未来计划', link: '/03-architecture/refactoring/future-plans' }
        ]
      }
    ]
  },

  // ========================================
  // 📚 API 参考
  // ========================================
  {
    text: '📚 API 参考',
    collapsed: true,
    items: [
      { text: 'API 参考总览', link: '/04-api-reference/README' },
      
      // UI 组件 API
      {
        text: 'UI 组件 API',
        collapsed: true,
        items: [
          { text: 'ColorVision.UI', link: '/04-api-reference/ui-components/ColorVision.UI' },
          { text: 'ColorVision.Common', link: '/04-api-reference/ui-components/ColorVision.Common' },
          { text: 'ColorVision.Core', link: '/04-api-reference/ui-components/ColorVision.Core' },
          { text: 'ColorVision.Themes', link: '/04-api-reference/ui-components/ColorVision.Themes' },
          { text: 'ColorVision.ImageEditor', link: '/04-api-reference/ui-components/ColorVision.ImageEditor' },
          { text: 'ColorVision.Solution', link: '/04-api-reference/ui-components/ColorVision.Solution' },
          { text: 'ColorVision.Scheduler', link: '/04-api-reference/ui-components/ColorVision.Scheduler' },
          { text: 'ColorVision.Database', link: '/04-api-reference/ui-components/ColorVision.Database' },
          { text: 'ColorVision.SocketProtocol', link: '/04-api-reference/ui-components/ColorVision.SocketProtocol' }
        ]
      },
      
      // Engine 组件 API
      {
        text: 'Engine 组件 API',
        collapsed: true,
        items: [
          { text: 'ColorVision.Engine', link: '/04-api-reference/engine-components/ColorVision.Engine' },
          { text: 'ColorVision.FileIO', link: '/04-api-reference/engine-components/ColorVision.FileIO' },
          { text: 'cvColorVision', link: '/04-api-reference/engine-components/cvColorVision' },
          { text: 'FlowEngineLib', link: '/04-api-reference/engine-components/FlowEngineLib' },
          { text: 'ST.Library.UI', link: '/04-api-reference/engine-components/ST.Library.UI' }
        ]
      },
      
      // 服务 API
      {
        text: '服务 API',
        collapsed: true,
        items: [
          { text: '设备服务', link: '/04-api-reference/services/device-services' },
          { text: '相机服务', link: '/04-api-reference/services/camera-service' },
          { text: '校准服务', link: '/04-api-reference/services/calibration-service' },
          { text: '电机服务', link: '/04-api-reference/services/motor-service' },
          { text: '文件服务', link: '/04-api-reference/services/file-service' },
          { text: 'SMU 服务', link: '/04-api-reference/services/smu-service' }
        ]
      },
      
      // 算法 API
      {
        text: '算法 API',
        collapsed: true,
        items: [
          { text: '算法概览', link: '/04-api-reference/algorithms/overview' },
          {
            text: '模板 API',
            collapsed: true,
            items: [
              { text: '模板基类', link: '/04-api-reference/algorithms/templates/template-base' },
              { text: 'POI 模板', link: '/04-api-reference/algorithms/templates/poi-template' },
              { text: 'ARVR 模板', link: '/04-api-reference/algorithms/templates/arvr-template' },
              { text: '自定义模板', link: '/04-api-reference/algorithms/templates/custom-template' }
            ]
          },
          {
            text: '算法原语',
            collapsed: true,
            items: [
              { text: 'ROI（感兴趣区域）', link: '/04-api-reference/algorithms/primitives/roi' },
              { text: 'POI（关注点）', link: '/04-api-reference/algorithms/primitives/poi' }
            ]
          },
          {
            text: '检测算法',
            collapsed: true,
            items: [
              { text: 'Ghost 检测', link: '/04-api-reference/algorithms/detectors/ghost-detection' },
              { text: '图案检测', link: '/04-api-reference/algorithms/detectors/pattern-detection' }
            ]
          }
        ]
      },
      
      // 插件 API
      {
        text: '插件 API',
        collapsed: true,
        items: [
          { text: '插件接口', link: '/04-api-reference/plugins/plugin-interface' },
          { text: '插件基类', link: '/04-api-reference/plugins/plugin-base' },
          {
            text: '标准插件',
            collapsed: true,
            items: [
              { text: 'Pattern 插件', link: '/04-api-reference/plugins/standard-plugins/pattern' },
              { text: '系统监控', link: '/04-api-reference/plugins/standard-plugins/system-monitor' },
              { text: '事件查看器', link: '/04-api-reference/plugins/standard-plugins/event-viewer' },
              { text: '屏幕录制', link: '/04-api-reference/plugins/standard-plugins/screen-recorder' }
            ]
          }
        ]
      },
      
      // 扩展点 API
      {
        text: '扩展点 API',
        collapsed: true,
        items: [
          { text: '属性编辑器扩展', link: '/04-api-reference/extensions/property-editor' },
          { text: '结果处理器', link: '/04-api-reference/extensions/result-handler' },
          { text: '绘图可视化', link: '/04-api-reference/extensions/drawing-visual' },
          { text: '配置提供者', link: '/04-api-reference/extensions/config-provider' }
        ]
      }
    ]
  },

  // ========================================
  // 📦 资源文档
  // ========================================
  {
    text: '📦 资源',
    collapsed: true,
    items: [
      { text: '资源总览', link: '/05-resources/README' },
      
      // 项目结构
      {
        text: '项目结构',
        collapsed: true,
        items: [
          { text: '结构总览', link: '/05-resources/project-structure/README' },
          { text: '模块文档对照', link: '/05-resources/project-structure/module-documentation-map' }
        ]
      },
      
      // 更新日志
      {
        text: '更新日志',
        collapsed: true,
        items: [
          { text: '更新日志', link: '/05-resources/changelog/README' },
          { text: '更新日志窗口', link: '/05-resources/changelog/window' }
        ]
      },
      
      // 术语表
      {
        text: '术语表',
        collapsed: true,
        items: [
          { text: '术语定义', link: '/05-resources/glossary/README' }
        ]
      },
      
      // 文档模板
      {
        text: '文档模板',
        collapsed: true,
        items: [
          { text: '通用文档模板', link: '/05-resources/templates/doc-template' },
          { text: 'API 文档模板', link: '/05-resources/templates/api-template' },
          { text: '教程模板', link: '/05-resources/templates/tutorial-template' }
        ]
      },
      
      // 法律文档
      {
        text: '法律文档',
        collapsed: true,
        items: [
          { text: '许可证', link: '/05-resources/legal/license' },
          { text: '软件许可协议', link: '/05-resources/legal/software-agreement' },
          { text: 'API v1.1', link: '/05-resources/legal/api-v1.1' }
        ]
      }
    ]
  }
]
```

**任务清单**：
- [ ] 9.1.1 备份当前 config.mts
- [ ] 9.1.2 更新侧边栏配置
- [ ] 9.1.3 测试导航链接
- [ ] 9.1.4 调整折叠状态

#### 9.2 更新导航栏配置

**导航栏建议**：

```typescript
nav: [
  { text: '首页', link: '/' },
  { text: '快速入门', link: '/00-getting-started/README' },
  { text: '用户指南', link: '/01-user-guide/README' },
  { text: '开发指南', link: '/02-developer-guide/README' },
  { text: 'API 参考', link: '/04-api-reference/README' },
  {
    text: '更多',
    items: [
      { text: '架构设计', link: '/03-architecture/README' },
      { text: '项目结构', link: '/05-resources/project-structure/README' },
      { text: '更新日志', link: 'https://github.com/xincheng213618/scgd_general_wpf/blob/master/CHANGELOG.md' },
      { text: 'GitHub', link: 'https://github.com/xincheng213618/scgd_general_wpf' }
    ]
  }
]
```

**任务清单**：
- [ ] 9.2.1 更新导航栏配置
- [ ] 9.2.2 测试下拉菜单

#### 9.3 更新 srcExclude 配置

```typescript
srcExclude: [
  '**/_*.md',           // 下划线开头的文件
  '**/.*',              // 隐藏文件
  'node_modules/**',    // node_modules
  '**/README.old.md',   // 备份文件
  '**/*.backup.md',     // 备份文件
  '**/TODO.md',         # 待办事项
  '**/DRAFT.md'         # 草稿文件
]
```

**任务清单**：
- [ ] 9.3.1 更新排除规则
- [ ] 9.3.2 测试构建

### 阶段十：质量检查与验证

#### 10.1 链接完整性检查

**检查清单**：
- [ ] 10.1.1 检查所有内部链接
- [ ] 10.1.2 检查所有图片链接
- [ ] 10.1.3 检查所有外部链接
- [ ] 10.1.4 修复损坏的链接
- [ ] 10.1.5 更新过时的链接

**检查工具**：
```bash
# 使用 markdown-link-check 或手动检查
npm install -g markdown-link-check
find docs -name "*.md" -exec markdown-link-check {} \;
```

#### 10.2 文档格式一致性检查

**检查项目**：
- [ ] 10.2.1 标题层级正确（从 H1 开始，不跳级）
- [ ] 10.2.2 代码块语言标记一致
- [ ] 10.2.3 列表格式统一
- [ ] 10.2.4 表格格式规范
- [ ] 10.2.5 图片 alt 文本完整
- [ ] 10.2.6 文件名统一（kebab-case）
- [ ] 10.2.7 中英文标点符号正确

#### 10.3 内容完整性检查

**检查清单**：
- [ ] 10.3.1 每个目录都有 README.md
- [ ] 10.3.2 所有 API 文档格式一致
- [ ] 10.3.3 所有教程有示例代码
- [ ] 10.3.4 所有配置有说明
- [ ] 10.3.5 术语使用一致

#### 10.4 VitePress 构建测试

**测试步骤**：
```bash
# 进入 docs 目录
cd docs

# 安装依赖
npm install

# 开发模式测试
npm run docs:dev

# 构建测试
npm run docs:build

# 预览构建结果
npm run docs:preview
```

**检查项目**：
- [ ] 10.4.1 开发模式正常启动
- [ ] 10.4.2 构建无错误无警告
- [ ] 10.4.3 所有页面能正常访问
- [ ] 10.4.4 导航功能正常
- [ ] 10.4.5 搜索功能正常
- [ ] 10.4.6 主题切换正常
- [ ] 10.4.7 移动端显示正常

#### 10.5 用户体验测试

**测试场景**：

**新用户路径**：
- [ ] 10.5.1 从首页到快速入门流畅
- [ ] 10.5.2 能快速找到安装指南
- [ ] 10.5.3 能找到常见问题解答

**开发者路径**：
- [ ] 10.5.4 能快速找到开发环境搭建
- [ ] 10.5.5 能快速定位 API 文档
- [ ] 10.5.6 能找到插件开发示例

**架构师路径**：
- [ ] 10.5.7 能快速了解系统架构
- [ ] 10.5.8 能找到设计模式说明
- [ ] 10.5.9 能查看架构图表

### 阶段十一：清理与优化

#### 11.1 删除旧目录

**待删除目录**（在确认新结构正常后）：

```bash
# 备份旧目录
mv docs/getting-started docs-backup/getting-started
mv docs/user-interface-guide docs-backup/user-interface-guide
mv docs/ui-components docs-backup/ui-components
# ... 其他旧目录
```

**任务清单**：
- [ ] 11.1.1 确认新结构完全可用
- [ ] 11.1.2 备份旧目录到 docs-backup
- [ ] 11.1.3 逐步删除旧目录
- [ ] 11.1.4 验证删除后构建正常

#### 11.2 优化文件大小

**检查项目**：
- [ ] 11.2.1 压缩过大的图片
- [ ] 11.2.2 删除未使用的资源
- [ ] 11.2.3 优化 Mermaid 图表

#### 11.3 添加文档元数据

**每个文档添加 Frontmatter**：
```yaml
---
title: 文档标题
description: 文档描述
outline: [2, 3]
---
```

**任务清单**：
- [ ] 11.3.1 为所有主要文档添加 frontmatter
- [ ] 11.3.2 设置合适的 outline 级别
- [ ] 11.3.3 添加适当的 description

### 阶段十二：文档维护指南

#### 12.1 文档更新流程

**新增文档**：
1. 确定文档类别（用户指南/开发指南/架构/API）
2. 在对应目录创建文档
3. 更新对应的 README 索引
4. 更新 VitePress 侧边栏配置
5. 测试链接和导航
6. 提交 PR

**修改文档**：
1. 修改文档内容
2. 更新修改日期
3. 检查内部链接
4. 测试构建
5. 提交 PR

**删除文档**：
1. 检查文档引用
2. 更新所有引用链接
3. 从侧边栏配置移除
4. 归档或删除文件
5. 测试构建
6. 提交 PR

#### 12.2 文档规范

**文件命名**：
- 使用 kebab-case（小写加连字符）
- 英文文件名（便于 URL）
- 有意义的描述性名称

**内容规范**：
- 标题从 H1 开始，不跳级
- 代码块使用语言标记
- 示例代码可运行
- 图片有 alt 文本
- 链接使用相对路径
- 术语使用一致

**格式规范**：
- 中英文间加空格
- 使用中文标点
- 列表符号统一
- 代码缩进一致

#### 12.3 定期检查任务

**每月检查**：
- [ ] 检查链接有效性
- [ ] 更新过时内容
- [ ] 检查构建错误
- [ ] 更新依赖版本

**每季度检查**：
- [ ] 审查文档结构
- [ ] 收集用户反馈
- [ ] 优化导航体系
- [ ] 补充缺失文档

## 📝 完整目录创建清单

### 所有目录创建命令

```bash
cd /home/runner/work/scgd_general_wpf/scgd_general_wpf/docs

# 一级目录
mkdir -p 00-getting-started
mkdir -p 01-user-guide
mkdir -p 02-developer-guide
mkdir -p 03-architecture
mkdir -p 04-api-reference
mkdir -p 05-resources

# 01-user-guide 子目录
mkdir -p 01-user-guide/interface
mkdir -p 01-user-guide/image-editor
mkdir -p 01-user-guide/devices
mkdir -p 01-user-guide/workflow
mkdir -p 01-user-guide/data-management
mkdir -p 01-user-guide/troubleshooting

# 02-developer-guide 子目录
mkdir -p 02-developer-guide/getting-started
mkdir -p 02-developer-guide/core-concepts
mkdir -p 02-developer-guide/ui-development
mkdir -p 02-developer-guide/engine-development
mkdir -p 02-developer-guide/plugin-development
mkdir -p 02-developer-guide/testing
mkdir -p 02-developer-guide/performance
mkdir -p 02-developer-guide/deployment

# 03-architecture 子目录
mkdir -p 03-architecture/overview
mkdir -p 03-architecture/layers
mkdir -p 03-architecture/components/engine
mkdir -p 03-architecture/components/ui
mkdir -p 03-architecture/components/plugins
mkdir -p 03-architecture/patterns
mkdir -p 03-architecture/data-flow
mkdir -p 03-architecture/security
mkdir -p 03-architecture/refactoring/engine-refactoring

# 04-api-reference 子目录
mkdir -p 04-api-reference/ui-components
mkdir -p 04-api-reference/engine-components
mkdir -p 04-api-reference/services
mkdir -p 04-api-reference/algorithms/templates
mkdir -p 04-api-reference/algorithms/primitives
mkdir -p 04-api-reference/algorithms/detectors
mkdir -p 04-api-reference/plugins/standard-plugins
mkdir -p 04-api-reference/extensions

# 05-resources 子目录
mkdir -p 05-resources/project-structure
mkdir -p 05-resources/changelog/migration-guides
mkdir -p 05-resources/glossary
mkdir -p 05-resources/templates
mkdir -p 05-resources/legal
mkdir -p 05-resources/assets/images
mkdir -p 05-resources/assets/diagrams
mkdir -p 05-resources/assets/downloads
```

## 🎯 总结

### 执行顺序建议

1. **准备阶段**（1-2 天）
   - 备份现有文档
   - 创建新目录结构
   - 准备迁移脚本

2. **迁移阶段**（3-5 天）
   - 按阶段执行文件迁移
   - 每完成一个阶段测试一次
   - 逐步更新 VitePress 配置

3. **验证阶段**（2-3 天）
   - 链接检查
   - 格式检查
   - 构建测试
   - 用户体验测试

4. **清理阶段**（1 天）
   - 删除旧目录
   - 优化文件
   - 最终测试

5. **发布阶段**（1 天）
   - 更新在线文档
   - 发布公告
   - 收集反馈

### 预期成果

✅ **统一的文档体验**：所有文档遵循一致的结构和规范  
✅ **清晰的导航**：用户可以快速找到所需文档  
✅ **消除重复**：每个主题只在一个地方详细说明  
✅ **易于维护**：文档结构清晰，便于更新  
✅ **高质量文档**：格式规范，内容完整，示例丰富  

---

**文档版本**: v1.0  
**创建日期**: 2025-11-03  
**状态**: 待执行

所有任务完成并勾选后，由用户确认删除此计划文档。
