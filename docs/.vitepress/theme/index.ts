// .vitepress/theme/index.ts
import { h } from 'vue'
import type { Theme } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import KnowledgeContext from './KnowledgeContext.vue'
import GridDistortionDemo from '../components/GridDistortionDemo.vue'
import './custom.css'
import '../algorithm-help/figures.css'

export default {
  extends: DefaultTheme,
  Layout: () => {
    return h(DefaultTheme.Layout, null, {
      'doc-before': () => h(KnowledgeContext),
    })
  },
  enhanceApp({ app, router, siteData }) {
    app.component('GridDistortionDemo', GridDistortionDemo)
  }
} satisfies Theme
