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
        { text: '设计与架构', link: '/03-architecture/README' },
        { text: 'API 参考', link: '/04-api-reference/README' },
        { text: '更新日志', link: 'https://github.com/xincheng213618/scgd_general_wpf/blob/master/CHANGELOG.md' },
        { text: 'GitHub', link: 'https://github.com/xincheng213618/scgd_general_wpf' }
      ],
      
      // Sidebar navigation
      sidebar: [
        {
          text: '开始',
          collapsed: false,
          items: [
            { text: '文档首页', link: '/' },
            { text: '入门指南', link: '/00-getting-started/README' },
            { text: '用户指南', link: '/01-user-guide/README' },
            { text: '开发指南', link: '/02-developer-guide/README' },
            { text: '架构设计', link: '/03-architecture/README' },
            { text: 'API 参考', link: '/04-api-reference/README' },
            { text: '附录与资源', link: '/05-resources/README' }
          ]
        },
        {
          text: '安装与首次使用',
          collapsed: false,
          items: [
            { text: '章节概览', link: '/00-getting-started/README' },
            { text: '什么是 ColorVision', link: '/00-getting-started/what-is-colorvision' },
            { text: '系统要求', link: '/00-getting-started/prerequisites' },
            { text: '安装指南', link: '/00-getting-started/installation' },
            { text: '首次运行', link: '/00-getting-started/first-steps' },
            { text: '快速上手', link: '/00-getting-started/quick-start' }
          ]
        },
        {
          text: '日常使用',
          collapsed: true,
          items: [
            { text: '章节概览', link: '/01-user-guide/README' },
            {
              text: '界面使用',
              collapsed: true,
              items: [
                { text: '主窗口导览', link: '/01-user-guide/interface/main-window' },
                { text: '属性编辑器', link: '/01-user-guide/interface/property-editor' },
                { text: '日志查看器', link: '/01-user-guide/interface/log-viewer' },
                { text: '终端', link: '/01-user-guide/interface/terminal' }
              ]
            },
            {
              text: '图像编辑器',
              link: '/01-user-guide/image-editor/overview'
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
          text: '开发与交付',
          collapsed: true,
          items: [
            { text: '章节概览', link: '/02-developer-guide/README' },
            {
              text: '核心概念',
              collapsed: true,
              items: [
                { text: '扩展性概览', link: '/02-developer-guide/core-concepts/extensibility' }
              ]
            },
            {
              text: 'Engine 开发总览',
              link: '/02-developer-guide/engine-development/README'
            },
            {
              text: '插件开发',
              link: '/02-developer-guide/plugin-development/README'
            },
            {
              text: '性能优化',
              collapsed: true,
              items: [
                { text: '性能优化指南', link: '/02-developer-guide/performance/overview' },
                { text: 'Socket 通信优化路线', link: '/02-developer-guide/performance/socket-protocol-optimization-roadmap' }
              ]
            },
            {
              text: '部署',
              collapsed: true,
              items: [
                { text: '部署概览', link: '/02-developer-guide/deployment/overview' },
                { text: '自动更新系统', link: '/02-developer-guide/deployment/auto-update' }
              ]
            },
            {
              text: '后端服务',
              collapsed: true,
              items: [
                { text: '插件市场后端', link: '/02-developer-guide/backend/README' }
              ]
            },
            {
              text: '构建脚本',
              collapsed: true,
              items: [
                { text: '构建与发布脚本', link: '/02-developer-guide/scripts/README' }
              ]
            }
          ]
        },
        {
          text: '设计与架构',
          collapsed: true,
          items: [
            { text: '章节概览', link: '/03-architecture/README' },
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
                { text: 'Templates 模块分析（补充）', link: '/03-architecture/components/templates/analysis' }
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
          text: 'API 参考',
          collapsed: true,
          items: [
            { text: '章节概览', link: '/04-api-reference/README' },
            {
              text: 'UI 组件总览',
              link: '/04-api-reference/ui-components/README'
            },
            {
              text: 'Engine 组件总览',
              link: '/04-api-reference/engine-components/README'
            },
            {
              text: '算法与模板',
              collapsed: true,
              items: [
                { text: '章节概览', link: '/04-api-reference/algorithms/README' },
                { text: '算法系统概览', link: '/04-api-reference/algorithms/overview' },
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
                    { text: 'POI 模板', link: '/04-api-reference/algorithms/templates/poi-template' },
                    { text: 'ARVR 模板', link: '/04-api-reference/algorithms/templates/arvr-template' },
                    { text: 'JSON 模板', link: '/04-api-reference/algorithms/templates/json-templates' },
                    { text: 'Templates API 参考', link: '/04-api-reference/algorithms/templates/api-reference' }
                  ]
                }
              ]
            },
            {
              text: '插件与现状页',
              collapsed: true,
              items: [
                { text: '章节概览', link: '/04-api-reference/plugins/README' },
                { text: 'Pattern / 图卡生成功能', link: '/04-api-reference/plugins/standard-plugins/pattern' },
                { text: 'ImageProjector（历史状态）', link: '/04-api-reference/plugins/standard-plugins/image-projector' },
                { text: 'ScreenRecorder（历史状态）', link: '/04-api-reference/plugins/standard-plugins/screen-recorder' },
                { text: 'Spectrum 插件', link: '/04-api-reference/plugins/standard-plugins/spectrum' },
                { text: 'SystemMonitor 插件', link: '/04-api-reference/plugins/standard-plugins/system-monitor' },
                { text: 'EventVWR 插件', link: '/04-api-reference/plugins/standard-plugins/eventvwr' },
                { text: 'Windows 服务插件', link: '/04-api-reference/plugins/standard-plugins/windows-service' }
              ]
            },
            {
              text: '扩展点概览',
              collapsed: true,
              items: [
                { text: '章节概览', link: '/04-api-reference/extensions/README' },
                { text: 'FlowEngineLib 节点扩展', link: '/04-api-reference/extensions/flow-node' }
              ]
            }
          ]
        },
        {
          text: '附录与资源',
          collapsed: true,
          items: [
            { text: '章节概览', link: '/05-resources/README' },
            { text: '项目结构总览', link: '/05-resources/project-structure/README' },
            { text: '模块与文档对照', link: '/05-resources/project-structure/module-documentation-map' },
            { text: '软件许可协议', link: '/05-resources/legal/software-agreement' }
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
      build: {
        chunkSizeWarningLimit: 2048
      },
      server: {
        port: 3000
      }
    }
  })
)
