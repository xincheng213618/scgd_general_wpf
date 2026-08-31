---
search: false
outline: false
redirect_from_deleted_page: true
---

<script setup>
import { onMounted } from 'vue'

onMounted(() => {
  window.location.replace('./design')
})
</script>

# 内容已合并

模板注册、参数与普通持久化统一维护在[模板核心契约](./design.md)；编辑和创建窗口从该主题进入对应宿主契约。不再按架构分析、API 参考和开发指南重复维护同一套事实。
