---
search: false
outline: false
redirect_from_deleted_page: true
---

<script setup>
import { onMounted } from 'vue'

onMounted(() => {
  window.location.replace('../02-developer-guide/core-concepts/copilot-agent-runtime')
})
</script>

# Copilot 知识已按实现职责合并

原使用手册不再单独维护。请从[Copilot 源码责任入口](../02-developer-guide/core-concepts/copilot-agent-runtime.md)定位设置、输入交互、任务执行、审批和业务扩展；这些主题与代码共同维护，网页只是同一份知识的展示。
