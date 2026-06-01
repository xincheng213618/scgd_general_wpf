import { defineConfig } from 'vitepress'
import { withMermaid } from "vitepress-plugin-mermaid"
import {
  buildSearchTranslations,
  buildVitePressLocales,
  defaultLocaleKey,
  editLinkPattern,
  footerCopyright,
  getLocaleDefinition,
  lastUpdatedFormatOptions,
} from './i18n/locales.mjs'
import { createNavItems, createSidebarItems } from './i18n/navigation.mjs'

const defaultLocale = getLocaleDefinition(defaultLocaleKey)

function createLocaleThemeConfig(localeKey: string) {
  const locale = getLocaleDefinition(localeKey)

  return {
    nav: createNavItems(localeKey),
    sidebar: createSidebarItems(localeKey),
    editLink: {
      pattern: editLinkPattern,
      text: locale.editLinkText,
    },
    footer: {
      message: locale.footerMessage,
      copyright: footerCopyright,
    },
    outline: {
      level: [2, 3],
      label: locale.outlineLabel,
    },
    docFooter: locale.docFooter,
    langMenuLabel: locale.langMenuLabel,
    darkModeSwitchLabel: locale.darkModeSwitchLabel,
    lightModeSwitchTitle: locale.lightModeSwitchTitle,
    darkModeSwitchTitle: locale.darkModeSwitchTitle,
    sidebarMenuLabel: locale.sidebarMenuLabel,
    returnToTopLabel: locale.returnToTopLabel,
    lastUpdated: {
      text: locale.lastUpdatedText,
      formatOptions: lastUpdatedFormatOptions,
    },
  }
}

// https://vitepress.dev/reference/site-config
export default withMermaid(
  defineConfig({
    title: defaultLocale.title,
    description: defaultLocale.description,
    lang: defaultLocale.lang,
    locales: buildVitePressLocales((localeKey) => createLocaleThemeConfig(localeKey)),
    
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

    themeConfig: {
      logo: '/images/ColorVision.png',
      siteTitle: 'ColorVision',
      socialLinks: [
        { icon: 'github', link: 'https://github.com/xincheng213618/scgd_general_wpf' }
      ],
      search: {
        provider: 'local',
        options: {
          locales: buildSearchTranslations()
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
