# Data Storage Documentation

数据存储系统文档，包括数据分类、存储架构和数据库设计。

## 目录结构

- [Storage Overview](storage-overview.md) - 存储系统概览，数据分类和存储策略
- [ER Model](er-model.md) - 实体关系模型和数据库结构
- [Table Indexing](table-indexing.md) - 数据库索引策略和性能优化

## 概述

ColorVision 数据存储系统支持多种数据类型和存储后端：

### 数据分类

- **配置数据**: 系统配置、用户偏好设置
- **运行日志**: 系统运行记录、错误日志
- **算法结果**: 图像处理结果、分析数据
- **模板数据**: 算法模板、流程配置
- **设备状态**: 硬件设备状态和参数

### 存储后端

- **SQLite**: 轻量级本地存储，适用于配置和小规模数据
- **MySQL**: 企业级数据库，适用于大规模生产环境
- **文件存储**: 图像文件、日志文件等非结构化数据

## 数据库组件

- `UI/ColorVision.UI/Database/` - 数据库访问层
- `Engine/ColorVision.Engine/Database/` - 数据模型和 ORM

## 相关文档

- [性能优化指南](../performance/README.md)
- [系统架构概览](../introduction/system-architecture/系统架构概览.md)

---

*最后更新: 2024-09-28*