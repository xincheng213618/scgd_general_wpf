# 工作流程

ColorVision 提供强大的可视化工作流程系统，基于 FlowEngine 实现。

## 概述

工作流程系统允许用户通过拖拽方式设计测试流程，无需编程即可完成复杂的自动化测试任务。

## 主要特性

- 🎨 可视化流程设计器
- 📦 丰富的节点类型
- 🔄 流程执行和调试
- 💾 流程模板管理
- 📊 执行结果可视化

## 核心概念

### 流程节点

流程由多个节点组成，每个节点代表一个操作：

- **设备节点**: 控制设备（相机、电机、光谱仪等）
- **算法节点**: 执行图像处理算法
- **逻辑节点**: 条件判断、循环等
- **数据节点**: 数据读写、转换等

### 节点连接

节点通过连线连接，定义执行顺序和数据流。

### 模板系统

模板是预配置的流程或节点，可重复使用。

## 使用指南

详见：

- [流程设计](./design.md) - 如何设计流程
- [流程执行](./execution.md) - 如何执行和调试流程

## 相关文档

- [FlowEngine 架构](/03-architecture/components/engine/flow-engine.md)
- [模板系统](/04-api-reference/algorithms/templates/template-management.md)
- [流程引擎 API](/04-api-reference/algorithms/templates/flow-engine.md)
