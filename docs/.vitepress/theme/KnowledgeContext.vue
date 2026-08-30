<script setup lang="ts">
import { computed } from 'vue'
import { useData } from 'vitepress'

const { frontmatter, page } = useData()
const labels: Record<string, string> = {
  current: '当前知识',
  planned: '规划 · 未落地',
  historical: '历史 · 非当前契约',
}
const status = computed(() => labels[frontmatter.value.status] ?? '状态待核对')
</script>

<template>
  <aside v-if="frontmatter.knowledge_id" class="knowledge-context" aria-label="知识来源与状态">
    <div class="knowledge-heading">
      <span class="knowledge-status" :class="frontmatter.status">{{ status }}</span>
      <code>{{ frontmatter.knowledge_id }}</code>
    </div>
    <p>{{ frontmatter.summary }}</p>
    <details>
      <summary>查看源码与测试映射</summary>
      <p class="knowledge-caveat">以下均为仓库相对路径。当前知识不等于默认启用；引用测试不代表测试已通过。修改前核对当前分支源码。</p>
      <dl>
        <dt>知识正文</dt>
        <dd><code>docs/{{ page.relativePath }}</code></dd>
        <dt>实现入口</dt>
        <dd v-for="source in frontmatter.code_paths" :key="source"><code>{{ source }}</code></dd>
        <dd v-if="!frontmatter.code_paths?.length">未声明实现入口。</dd>
        <dt>验证入口</dt>
        <dd v-for="test in frontmatter.test_paths" :key="test"><code>{{ test }}</code></dd>
        <dd v-if="!frontmatter.test_paths?.length">未声明自动化覆盖；验证缺口见正文。</dd>
      </dl>
    </details>
  </aside>
</template>

<style scoped>
.knowledge-context {
  margin-bottom: 24px;
  padding: 16px 20px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  background: var(--vp-c-bg-soft);
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 1.7;
  overflow-wrap: anywhere;
}
.knowledge-heading { display: flex; align-items: baseline; flex-wrap: wrap; gap: 8px 12px; }
.knowledge-heading code { font-size: 12px; }
.knowledge-status { font-weight: 600; color: var(--vp-c-text-1); }
.knowledge-status.planned, .knowledge-status.historical { color: var(--vp-c-warning-1); }
.knowledge-context p { margin: 8px 0; }
.knowledge-context summary { cursor: pointer; color: var(--vp-c-brand-1); }
.knowledge-context summary:focus-visible { outline: 2px solid var(--vp-c-brand-1); outline-offset: 4px; }
.knowledge-caveat { font-size: 12px; }
.knowledge-context dl { margin: 12px 0 0; }
.knowledge-context dt { margin-top: 8px; color: var(--vp-c-text-1); font-weight: 600; }
.knowledge-context dd { margin: 2px 0; font-size: 12px; }
</style>
