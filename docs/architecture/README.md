# Architecture Documentation

This section contains comprehensive architectural documentation for the ColorVision system.

## 目录结构

- [Architecture Runtime](architecture-runtime.md) - 系统运行时架构，包括启动序列和组件交互
- [Component Interactions](component-interactions.md) - 模块交互矩阵和依赖关系
- [Component Map](component-map.json) - 组件映射的 JSON 结构

## 概述

ColorVision 系统采用模块化设计，由以下主要层次组成：

- **引擎层 (Engine)**: 核心算法和业务逻辑
- **UI 层**: 用户界面组件和交互
- **插件层**: 可扩展的功能模块
- **数据层**: 数据存储和管理
- **通信层**: MQTT 和网络通信

## 相关文档

- [系统架构概览](../introduction/system-architecture/系统架构概览.md)
- [核心组件](../engine-components/Engine组件概览.md)
- [插件系统](../plugins/plugin-management/插件管理.md)

---

*最后更新: 2024-09-28*