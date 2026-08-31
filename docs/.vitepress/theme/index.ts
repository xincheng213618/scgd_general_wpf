// .vitepress/theme/index.ts
import { h } from 'vue'
import type { Theme } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import KnowledgeContext from './KnowledgeContext.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  Layout: () => {
    return h(DefaultTheme.Layout, null, {
      'doc-before': () => h(KnowledgeContext),
    })
  },
  enhanceApp({ app, router, siteData }) {
    // App level customizations can be added here
  }
} satisfies Theme
