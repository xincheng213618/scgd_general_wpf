# Architecture Documentation

This section contains comprehensive architectural documentation for the ColorVision system.

## 目录结构

### 系统架构文档
- [Architecture Runtime](architecture-runtime.md) - 系统运行时架构，包括启动序列和组件交互
- [Component Interactions](component-interactions.md) - 模块交互矩阵和依赖关系
- [Component Map](component-map.json) - 组件映射的 JSON 结构
- [FlowEngineLib Architecture](FlowEngineLib-Architecture.md) - 流程引擎库架构设计

### ColorVision.Engine 重构计划 🚀
- **[重构项目README](ColorVision.Engine-Refactoring-README.md)** - 📖 项目总览和文档导航（推荐首先阅读）
- [完整重构方案](ColorVision.Engine-Refactoring-Plan.md) - Engine DLL 拆分和优化完整技术方案（32KB，1315行）
- [执行摘要](ColorVision.Engine-Refactoring-Summary.md) - 快速参考和核心要点（5.5KB，219行）
- [架构图表](ColorVision.Engine-Refactoring-Diagrams.md) - 可视化架构设计和流程图（13KB，包含Mermaid图表）
- [实施检查清单](ColorVision.Engine-Refactoring-Checklist.md) - 详细的任务列表和进度跟踪（13KB，516行）

## 概述

ColorVision 系统采用模块化设计，由以下主要层次组成：

- **引擎层 (Engine)**: 核心算法和业务逻辑
- **UI 层**: 用户界面组件和交互
- **插件层**: 可扩展的功能模块
- **数据层**: 数据存储和管理
- **通信层**: MQTT 和网络通信

### ColorVision.Engine 重构概览

ColorVision.Engine 是系统的核心引擎，目前包含580+文件的单体DLL。重构计划将其拆分为9个独立模块：

**核心层**:
- `ColorVision.Engine.Core` - 核心接口和抽象（~60文件）

**业务层**:
- `ColorVision.Engine.Flow` - 流程引擎（~50文件）
- `ColorVision.Engine.Templates` - 模板系统（~220文件）
- `ColorVision.Engine.Devices` - 设备服务（~160文件）
- `ColorVision.Engine.Algorithms` - 算法引擎（~50文件）
- `ColorVision.Engine.PhysicalDevices` - 物理设备管理（~25文件）

**基础设施层**:
- `ColorVision.Engine.Data` - 数据访问（~70文件）
- `ColorVision.Engine.Communication` - 通信层（~40文件）
- `ColorVision.Engine.Infrastructure` - 基础设施（~50文件）

**预期收益**:
- 启动时间减少40%
- 内存占用降低30%
- 开发效率提升40%
- 单元测试覆盖率达到80%+

**实施周期**: 3-4个月（10个阶段）

详细信息请参考上述重构计划文档。

## 相关文档

- [系统架构概览](../introduction/system-architecture/系统架构概览.md)
- [核心组件](../engine-components/Engine组件概览.md)
- [插件系统](../plugins/plugin-management/插件管理.md)

---

*最后更新: 2025-01-08*