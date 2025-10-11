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
    
    // Theme configuration
    themeConfig: {
      // Site branding
      logo: '/UI.png',
      siteTitle: 'ColorVision',
      
      // Navigation
      nav: [
        { text: '首页', link: '/' },
        { text: '入门指南', link: '/getting-started/入门指南' },
        { text: '架构', link: '/introduction/system-architecture/系统架构概览' },
        { text: 'GitHub', link: 'https://github.com/xincheng213618/scgd_general_wpf' }
      ],
      
      // Sidebar navigation
      sidebar: [
        {
          text: '🚀 入门',
          collapsed: false,
          items: [
            { text: '项目简介', link: '/introduction/简介' },
            { text: '什么是 ColorVision', link: '/introduction/what-is-colorvision/什么是_ColorVision_' },
            { text: '主要特性', link: '/introduction/key-features/主要特性' },
            { text: '入门指南', link: '/getting-started/入门指南' },
            { text: '快速上手', link: '/getting-started/quick-start/快速上手' },
            { text: '系统要求', link: '/getting-started/prerequisites/系统要求' },
            { text: '安装指南', link: '/getting-started/installation/安装_ColorVision' }
          ]
        },
        {
          text: '🏗️ 架构与模块',
          collapsed: false,
          items: [
            { text: '系统架构概览', link: '/introduction/system-architecture/系统架构概览' },
            { text: '架构运行时', link: '/architecture/architecture-runtime' },
            { text: '组件交互矩阵', link: '/architecture/component-interactions' },
            {
              text: 'UI组件',
              collapsed: true,
              items: [
                { text: 'UI组件概览', link: '/ui-components/UI组件概览' },
                { text: 'ColorVision.UI', link: '/ui-components/ColorVision.UI' },
                { text: 'ColorVision.Common', link: '/ui-components/ColorVision.Common' },
                { text: 'ColorVision.Core', link: '/ui-components/ColorVision.Core' },
                { text: 'ColorVision.Themes', link: '/ui-components/ColorVision.Themes' },
                { text: 'ColorVision.ImageEditor', link: '/ui-components/ColorVision.ImageEditor' },
                { text: 'ColorVision.Solution', link: '/ui-components/ColorVision.Solution' },
                { text: 'ColorVision.Scheduler', link: '/ui-components/ColorVision.Scheduler' },
                { text: 'ColorVision.Database', link: '/ui-components/ColorVision.Database' },
                { text: 'ColorVision.SocketProtocol', link: '/ui-components/ColorVision.SocketProtocol' }
              ]
            },
            {
              text: 'Engine组件',
              collapsed: true,
              items: [
                { text: 'Engine组件概览', link: '/engine-components/Engine组件概览' },
                { text: 'ColorVision.Engine', link: '/engine-components/ColorVision.Engine' },
                { text: 'ColorVision.FileIO', link: '/engine-components/ColorVision.FileIO' },
                { text: 'cvColorVision', link: '/engine-components/cvColorVision' }
              ]
            }
          ]
        },
        {
          text: '🔌 插件系统',
          collapsed: true,
          items: [
            { text: '插件管理', link: '/plugins/plugin-management/插件管理' },
            { text: '使用标准插件', link: '/plugins/using-standard-plugins/使用标准插件' },
            { text: '插件生命周期', link: '/plugins/plugin-lifecycle' },
            { text: '开发插件指南', link: '/plugins/developing-a-plugin' }
          ]
        },
        {
          text: '⚙️ 流程引擎与算法',
          collapsed: true,
          items: [
            { text: '流程引擎', link: '/algorithm-engine-templates/flow-engine/流程引擎' },
            { text: '流程引擎概览', link: '/flow-engine/flow-engine-overview' },
            { text: '状态模型', link: '/flow-engine/state-model' },
            { text: '扩展点', link: '/flow-engine/extensibility-points' },
            { text: '算法引擎与模板', link: '/algorithm-engine-templates/算法引擎与模板' },
            { text: '模板管理', link: '/algorithm-engine-templates/template-management/模板管理' },
            { text: '基于JSON的通用模板', link: '/algorithm-engine-templates/json-based-templates/基于JSON的通用模板' },
            { text: '通用算法模块', link: '/algorithm-engine-templates/common-algorithm-primitives/通用算法模块' },
            { text: '特定领域算法模板', link: '/algorithm-engine-templates/specialized-algorithms/特定领域算法模板' },
            {
              text: '通用算法原语',
              collapsed: true,
              items: [
                { text: 'ROI (感兴趣区域)', link: '/common-algorithm-primitives/roi-region-of-interest/ROI_(感兴趣区域)' },
                { text: 'POI (关注点)', link: '/common-algorithm-primitives/poi-point-of-interest/POI_(关注点)' }
              ]
            }
          ]
        },
        {
          text: '📱 设备管理',
          collapsed: true,
          items: [
            { text: '设备服务概览', link: '/device-management/device-services-overview/设备服务概览' },
            { text: '添加与配置设备', link: '/device-management/adding-configuring-devices/添加与配置设备' },
            {
              text: '专用服务',
              collapsed: true,
              items: [
                { text: '相机服务', link: '/device-management/camera-service/相机服务' },
                { text: '校准服务', link: '/device-management/calibration-service/校准服务' },
                { text: '电机服务', link: '/device-management/motor-service/电机服务' },
                { text: '文件服务', link: '/device-management/file-server-service/文件服务' },
                { text: '流程设备服务', link: '/device-management/flow-device-service/流程设备服务' },
                { text: '源测量单元 (SMU) 服务', link: '/device-management/source-measure-unit-smu-service/源测量单元_(SMU)_服务' }
              ]
            }
          ]
        },
        {
          text: '🖥️ 用户界面',
          collapsed: true,
          items: [
            { text: '主窗口导览', link: '/user-interface-guide/main-window/主窗口导览' },
            { text: '图像编辑器', link: '/user-interface-guide/image-editor/图像编辑器' },
            { text: '属性编辑器', link: '/user-interface-guide/property-editor/属性编辑器' },
            { text: '日志查看器', link: '/user-interface-guide/log-viewer/日志查看器' }
          ]
        },
        {
          text: '📚 开发指南',
          collapsed: true,
          items: [
            { text: '故障排除', link: '/troubleshooting/故障排除' },
            { text: '常见问题与解决方案', link: '/troubleshooting/common-issues/常见问题与解决方案' },
            { text: '性能优化指南', link: '/performance/' },
            { text: '扩展性开发', link: '/extensibility/' },
            { text: '安全与权限控制', link: '/security/' },
            { text: 'RBAC 模型', link: '/rbac/rbac-model' },
            { text: 'API 参考', link: '/developer-guide/api-reference/API_参考' },
            { text: 'ColorVision API V1.1', link: '/ColorVision API V1.1' }
          ]
        },
        {
          text: '📦 部署与更新',
          collapsed: true,
          items: [
            { text: '数据存储概览', link: '/data-storage/' },
            { text: '部署文档', link: '/deployment/' },
            { text: '更新日志', link: '/changelog/' },
            { text: '自动更新', link: '/update/' },
            { text: '更新日志窗口', link: '/update/changelog-window' }
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
        copyright: 'Copyright © 2024-present ColorVision'
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
      ['link', { rel: 'icon', href: '/scgd_general_wpf/favicon.ico' }],
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
