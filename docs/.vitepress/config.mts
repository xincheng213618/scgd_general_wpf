import { defineConfig } from 'vitepress'
import { withMermaid } from "vitepress-plugin-mermaid"

// https://vitepress.dev/reference/site-config
export default withMermaid(
  defineConfig({
    title: "ColorVision",
    description: "ColorVision 是基于 Windows WPF 的专业光电技术解决方案，提供设备集成、流程自动化、色彩管理等功能",
    lang: 'zh-CN',
    
    // Base path for GitHub Pages deployment  
    base: '/scgd_general_wpf/',
    
    // Clean URLs (removes .html extension)
    cleanUrls: true,
    
    // Ignore dead links during build
    ignoreDeadLinks: true,
    
    // Ignore specific files/patterns
    srcExclude: [
      '**/_*.md',
      '**/.*',
      'node_modules/**'
    ],
    
    // Theme configuration
    themeConfig: {
      // Site branding
      logo: '/images/ColorVision.png',
      siteTitle: 'ColorVision',
      
      // Navigation
      nav: [
        { text: '首页', link: '/' },
        { text: '入门指南', link: '/00-getting-started/README' },
        { text: '用户指南', link: '/01-user-guide/README' },
        { text: '开发指南', link: '/02-developer-guide/README' },
        { text: '架构设计', link: '/03-architecture/README' },
        { text: '更新日志', link: 'https://github.com/xincheng213618/scgd_general_wpf/blob/master/CHANGELOG.md' },
        { text: 'GitHub', link: 'https://github.com/xincheng213618/scgd_general_wpf' }
      ],
      
      // Sidebar navigation
      sidebar: [
        {
          text: '📚 快速入门',
          collapsed: false,
          items: [
            { text: '什么是 ColorVision', link: '/00-getting-started/what-is-colorvision' },
            { text: '主要特性', link: '/00-getting-started/introduction/key-features' },
            { text: '系统架构概览', link: '/00-getting-started/introduction/system-architecture' },
            { text: '快速开始', link: '/00-getting-started/quick-start' },
            { text: '系统要求', link: '/00-getting-started/prerequisites' },
            { text: '安装指南', link: '/00-getting-started/installation' },
            { text: '首次运行指南', link: '/00-getting-started/first-steps' }
          ]
        },
        {
          text: '📖 用户指南',
          collapsed: false,
          items: [
            {
              text: '界面使用',
              collapsed: true,
              items: [
                { text: '主窗口导览', link: '/01-user-guide/interface/main-window' },
                { text: '属性编辑器', link: '/01-user-guide/interface/property-editor' },
                { text: '日志查看器', link: '/01-user-guide/interface/log-viewer' }
              ]
            },
            {
              text: '图像编辑器',
              collapsed: true,
              items: [
                { text: '图像编辑器概览', link: '/01-user-guide/image-editor/overview' }
              ]
            },
            {
              text: '设备管理',
              collapsed: true,
              items: [
                { text: '设备服务概览', link: '/01-user-guide/devices/overview' },
                { text: '添加与配置设备', link: '/01-user-guide/devices/configuration' },
                { text: '相机服务', link: '/01-user-guide/devices/camera' },
                { text: '相机管理', link: '/01-user-guide/devices/camera-management' },
                { text: '相机参数配置', link: '/01-user-guide/devices/camera-configuration' },
                { text: '电机服务', link: '/01-user-guide/devices/motor' },
                { text: '校准服务', link: '/01-user-guide/devices/calibration' },
                { text: 'SMU 服务', link: '/01-user-guide/devices/smu' },
                { text: '流程设备服务', link: '/01-user-guide/devices/flow-device' },
                { text: '文件服务器', link: '/01-user-guide/devices/file-server' }
              ]
            },
            {
              text: '工作流程',
              collapsed: true,
              items: [
                { text: '工作流程概览', link: '/01-user-guide/workflow/README' },
                { text: '流程设计', link: '/01-user-guide/workflow/design' },
                { text: '流程执行与调试', link: '/01-user-guide/workflow/execution' }
              ]
            },
            {
              text: '数据管理',
              collapsed: true,
              items: [
                { text: '数据管理概览', link: '/01-user-guide/data-management/README' },
                { text: '数据库操作', link: '/01-user-guide/data-management/database' },
                { text: '数据导出与导入', link: '/01-user-guide/data-management/export-import' }
              ]
            },
            {
              text: '故障排查',
              collapsed: true,
              items: [
                { text: '常见问题', link: '/01-user-guide/troubleshooting/common-issues' }
              ]
            }
          ]
        },
        {
          text: '👨‍💻 开发指南',
          collapsed: false,
          items: [
            {
              text: '核心概念',
              collapsed: true,
              items: [
                { text: '扩展性概览', link: '/02-developer-guide/core-concepts/extensibility' }
              ]
            },
            {
              text: 'UI 开发',
              collapsed: true,
              items: [
                { text: 'UI 开发概览', link: '/02-developer-guide/ui-development/README' },
                { text: 'XAML 与 MVVM', link: '/02-developer-guide/ui-development/xaml-mvvm' },
                { text: 'PropertyGrid 系统', link: '/02-developer-guide/ui-development/property-grid' },
                { text: '自定义控件', link: '/02-developer-guide/ui-development/custom-controls' },
                { text: 'ImageEditor 集成', link: '/02-developer-guide/ui-development/image-editor-integration' },
                { text: '主题与样式', link: '/02-developer-guide/ui-development/themes' }
              ]
            },
            {
              text: 'Engine 开发',
              collapsed: true,
              items: [
                { text: 'Engine 开发概览', link: '/02-developer-guide/engine-development/README' },
                { text: '服务开发', link: '/02-developer-guide/engine-development/services' },
                { text: '模板系统开发', link: '/02-developer-guide/engine-development/templates' },
                { text: 'MQTT 消息处理', link: '/02-developer-guide/engine-development/mqtt' },
                { text: 'OpenCV 集成', link: '/02-developer-guide/engine-development/opencv-integration' }
              ]
            },
            {
              text: '插件开发',
              collapsed: true,
              items: [
                { text: '插件开发概览', link: '/02-developer-guide/plugin-development/overview' },
                { text: '插件开发入门', link: '/02-developer-guide/plugin-development/getting-started' },
                { text: '插件生命周期', link: '/02-developer-guide/plugin-development/lifecycle' }
              ]
            },
            {
              text: '性能优化',
              collapsed: true,
              items: [
                { text: '性能优化指南', link: '/02-developer-guide/performance/overview' }
              ]
            },
            {
              text: '部署',
              collapsed: true,
              items: [
                { text: '部署概览', link: '/02-developer-guide/deployment/overview' },
                { text: '自动更新系统', link: '/02-developer-guide/deployment/auto-update' }
              ]
            }
          ]
        },
        {
          text: '🏗️ 架构设计',
          collapsed: false,
          items: [
            { text: '架构总览', link: '/03-architecture/README' },
            {
              text: '系统概览',
              collapsed: true,
              items: [
                { text: '系统架构概览', link: '/03-architecture/overview/system-overview' },
                { text: '架构运行时', link: '/03-architecture/overview/runtime' },
                { text: '组件交互', link: '/03-architecture/overview/component-interactions' }
              ]
            },
            {
              text: '组件架构',
              collapsed: true,
              items: [
                { text: 'FlowEngineLib 架构', link: '/03-architecture/components/engine/flow-engine' },
                { text: 'Templates 架构设计', link: '/03-architecture/components/templates/design' },
                { text: 'Templates 分析总结', link: '/03-architecture/components/templates/analysis' }
              ]
            },
            {
              text: '安全与权限',
              collapsed: true,
              items: [
                { text: '安全概览', link: '/03-architecture/security/overview' },
                { text: 'RBAC 模型', link: '/03-architecture/security/rbac' }
              ]
            }
          ]
        },
        {
          text: '📚 API 参考',
          collapsed: true,
          items: [
            {
              text: 'UI 组件 API',
              collapsed: true,
              items: [
                { text: 'UI 组件概览', link: '/04-api-reference/ui-components/README' },
                { text: 'ColorVision.UI', link: '/04-api-reference/ui-components/ColorVision.UI' },
                { text: 'ColorVision.UI.Desktop', link: '/04-api-reference/ui-components/ColorVision.UI.Desktop' },
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
            {
              text: 'Engine 组件 API',
              collapsed: true,
              items: [
                { text: 'Engine 组件概览', link: '/04-api-reference/engine-components/README' },
                { text: 'ColorVision.Engine', link: '/04-api-reference/engine-components/ColorVision.Engine' },
                { text: 'ColorVision.FileIO', link: '/04-api-reference/engine-components/ColorVision.FileIO' },
                { text: 'cvColorVision', link: '/04-api-reference/engine-components/cvColorVision' },
                { text: 'FlowEngineLib', link: '/04-api-reference/engine-components/FlowEngineLib' },
                { text: 'ST.Library.UI', link: '/04-api-reference/engine-components/ST.Library.UI' }
              ]
            },
            {
              text: '算法 API',
              collapsed: true,
              items: [
                { text: '算法概览', link: '/04-api-reference/algorithms/README' },
                { text: '算法总览', link: '/04-api-reference/algorithms/overview' },
                {
                  text: '检测器',
                  collapsed: true,
                  items: [
                    { text: 'Ghost 检测', link: '/04-api-reference/algorithms/detectors/ghost-detection' }
                  ]
                },
                {
                  text: '算法原语',
                  collapsed: true,
                  items: [
                    { text: 'ROI（感兴趣区域）', link: '/04-api-reference/algorithms/primitives/roi' },
                    { text: 'POI（关注点）', link: '/04-api-reference/algorithms/primitives/poi' },
                    { text: '通用算法模块', link: '/04-api-reference/algorithms/primitives/common-modules' }
                  ]
                },
                {
                  text: '模板系统',
                  collapsed: true,
                  items: [
                    { text: '流程引擎', link: '/04-api-reference/algorithms/templates/flow-engine' },
                    { text: '模板管理', link: '/04-api-reference/algorithms/templates/template-management' },
                    { text: 'POI 模板详解', link: '/04-api-reference/algorithms/templates/poi-template' },
                    { text: 'ARVR 模板详解', link: '/04-api-reference/algorithms/templates/arvr-template' },
                    { text: 'JSON 模板', link: '/04-api-reference/algorithms/templates/json-templates' },
                    { text: 'Templates API 参考', link: '/04-api-reference/algorithms/templates/api-reference' }
                  ]
                }
              ]
            },
            {
              text: '插件 API',
              collapsed: true,
              items: [
                { text: 'Pattern 插件', link: '/04-api-reference/plugins/standard-plugins/pattern' },
                { text: 'Spectrum 插件', link: '/04-api-reference/plugins/standard-plugins/spectrum' },
                { text: 'SystemMonitor 插件', link: '/04-api-reference/plugins/standard-plugins/system-monitor' }
              ]
            },
            {
              text: '扩展点 API',
              collapsed: true,
              items: [
                { text: 'FlowNode 开发', link: '/04-api-reference/extensions/flow-node' }
              ]
            }
          ]
        },
        {
          text: '📦 资源文档',
          collapsed: true,
          items: [
            {
              text: '项目结构',
              collapsed: true,
              items: [
                { text: '项目结构总览', link: '/05-resources/project-structure/README' },
                { text: '模块与文档对照', link: '/05-resources/project-structure/module-documentation-map' }
              ]
            },
            {
              text: '更新日志',
              collapsed: true,
              items: [
                { text: '更新日志窗口', link: '/05-resources/changelog/window' }
              ]
            },
            {
              text: '法律文档',
              collapsed: true,
              items: [
                { text: 'ColorVision API V1.1', link: '/05-resources/legal/api-v1.1' },
                { text: '软件许可协议', link: '/05-resources/legal/software-agreement' }
              ]
            },
            {
              text: '文档模板',
              collapsed: true,
              items: [
                { text: '文档模板', link: '/05-resources/templates/doc-template' }
              ]
            },
            { text: '数据存储说明', link: '/05-resources/data-storage' }
          ]
        }
      ],
      
      // Social links
      socialLinks: [
        { icon: 'github', link: 'https://github.com/xincheng213618/scgd_general_wpf' }
      ],
      
      // Search configuration
      search: {
        provider: 'local',
        options: {
          locales: {
            root: {
              translations: {
                button: {
                  buttonText: '搜索文档',
                  buttonAriaLabel: '搜索文档'
                },
                modal: {
                  noResultsText: '无结果',
                  resetButtonTitle: '清除查询条件',
                  footer: {
                    selectText: '选择',
                    navigateText: '切换'
                  }
                }
              }
            }
          }
        }
      },
      
      // Edit link configuration
      editLink: {
        pattern: 'https://github.com/xincheng213618/scgd_general_wpf/edit/master/docs/:path',
        text: '在 GitHub 上编辑此页'
      },
      
      // Footer
      footer: {
        message: 'Released under the MIT License.',
        copyright: 'Copyright © 2025-present ColorVision Development Team'
      },
      
      // Outline (table of contents) configuration
      outline: {
        level: [2, 3],
        label: '页面导航'
      },
      
      // Prev/Next links text
      docFooter: {
        prev: '上一章节',
        next: '下一章节'
      },
      
      // Dark mode switch label
      darkModeSwitchLabel: '主题',
      lightModeSwitchTitle: '切换到浅色模式',
      darkModeSwitchTitle: '切换到深色模式',
      sidebarMenuLabel: '菜单',
      returnToTopLabel: '返回顶部',
      
      // Last updated text
      lastUpdated: {
        text: '最后更新于',
        formatOptions: {
          dateStyle: 'short',
          timeStyle: 'short'
        }
      }
    },
    
    // Markdown configuration
    markdown: {
      lineNumbers: true,
      theme: {
        light: 'github-light',
        dark: 'github-dark'
      }
    },
    
    // Mermaid configuration
    mermaid: {
      theme: 'default'
    },
    
    // Head tags
    head: [
      ['link', { rel: 'icon', href: '/favicon.ico' }],
      ['meta', { name: 'theme-color', content: '#0969da' }],
      ['meta', { name: 'og:type', content: 'website' }],
      ['meta', { name: 'og:locale', content: 'zh_CN' }],
      ['meta', { name: 'og:title', content: 'ColorVision - 光电技术与色彩管理平台' }],
      ['meta', { name: 'og:site_name', content: 'ColorVision' }]
    ],
    
    // Vue app level enhancements
    vite: {
      server: {
        port: 3000
      }
    }
  })
)
