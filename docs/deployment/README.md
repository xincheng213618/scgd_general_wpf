# Deployment Documentation

系统部署文档，包括安装、配置和运维指南。

## 目录结构

- [Installation Guide](installation-guide.md) - 详细安装指南
- [Configuration](configuration.md) - 系统配置说明
- [Environment Setup](environment-setup.md) - 环境准备和依赖配置
- [Docker Deployment](docker-deployment.md) - 容器化部署方案
- [Production Setup](production-setup.md) - 生产环境部署最佳实践

## 概述

ColorVision 支持多种部署方式和环境：

### 部署方式

- **单机部署**: 适用于开发和测试环境
- **分布式部署**: 适用于大规模生产环境
- **容器部署**: 使用 Docker 进行标准化部署
- **云部署**: 支持主流云平台部署

### 系统要求

- **操作系统**: Windows 10/11, Windows Server 2019+
- **运行时**: .NET 8.0+ Runtime
- **数据库**: SQLite (本地) / MySQL 8.0+ (生产)
- **硬件**: 参考[系统要求](../getting-started/prerequisites/系统要求.md)

### 网络架构

- **MQTT Broker**: 消息队列服务
- **Database Server**: 数据库服务器
- **File Server**: 文件存储服务
- **Load Balancer**: 负载均衡器 (可选)

## 相关组件

- `Scripts/` - 部署和构建脚本
- `ColorVisionSetup/` - 安装程序项目

## 相关文档

- [安装指南](../getting-started/installation/安装_ColorVision.md)
- [系统要求](../getting-started/prerequisites/系统要求.md)
- [性能优化指南](../performance/README.md)

---

*最后更新: 2024-09-28*