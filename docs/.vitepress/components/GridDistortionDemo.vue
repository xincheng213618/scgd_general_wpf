<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { mountGridDistortionDemo } from './grid-distortion-demo.mjs'
import './grid-distortion-demo.css'

const container = ref(null)
let dispose
onMounted(() => { dispose = mountGridDistortionDemo(container.value) })
onBeforeUnmount(() => { dispose?.() })
</script>

<template>
  <section ref="container" aria-label="点阵畸变交互示意" />
</template>
