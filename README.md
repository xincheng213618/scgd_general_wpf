# ColorVision

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/xincheng213618/scgd_general_wpf)
![.NET Version](https://img.shields.io/badge/.NET-8.0-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)
![WPF](https://img.shields.io/badge/UI-WPF-blue.svg)
![License](https://img.shields.io/github/license/xincheng213618/scgd_general_wpf.svg)
![Stars](https://img.shields.io/github/stars/xincheng213618/scgd_general_wpf.svg)

## 📋 项目简介

ColorVision 是一款基于 WPF 的专业视觉检测平台，采用模块化架构设计，支持多框架协同工作。专注于提供高效、精准的图像处理及分析功能，适用于光电技术、色彩管理、质量检测等应用场景。

**📚 完整文档**: https://xincheng213618.github.io/scgd_general_wpf/

## ✨ 核心特性

- **🎨 多主题支持** - 深色、浅色、粉色、青色主题，支持跟随系统
- **🌐 多语言国际化** - English、简体中文、繁体中文、日本語、한国어
- **🔌 灵活插件机制** - 支持插件热加载和扩展功能开发
- **⚡ 流程引擎** - 可视化流程编辑器，支持算法模板和服务配置
- **📷 设备集成** - 支持多种相机、光谱仪、传感器等设备
- **🔄 自动更新系统** - 增量更新、签名验证、自动回滚机制
- **📝 PropertyEditor** - 支持多种集合类型（List、ObservableCollection、Dictionary等）的动态属性编辑

📖 [查看完整特性列表 →](docs/00-getting-started/introduction/key-features.md)

## 🚀 快速开始

### 环境要求

- **.NET 8.0**
- **Windows 10 1903+** 或 **Windows 11**
- **Visual Studio 2026** (开发环境)

### 构建与运行

```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行主程序
dotnet run --project ColorVision/ColorVision.csproj
```

📖 [完整入门指南 →](docs/00-getting-started/README.md)

## 📁 项目结构

```
ColorVision/
├── ColorVision/              # 主程序入口
├── Engine/                   # 核心引擎层
│   ├── ColorVision.Engine/   # 主引擎模块
│   ├── cvColorVision/        # 视觉处理核心
│   ├── FlowEngineLib/        # 流程引擎库
│   └── ColorVision.FileIO/   # 文件IO处理
├── UI/                       # 用户界面层
│   ├── ColorVision.UI/       # 主UI框架
│   ├── ColorVision.Themes/   # 主题管理
│   ├── ColorVision.ImageEditor/  # 图像编辑器
│   └── ColorVision.*/        # 其他UI模块
├── Plugins/                  # 扩展插件
│   ├── EventVWR/            # 事件查看器
│   ├── SystemMonitor/       # 系统监控
│   └── WindowsServicePlugin/ # Windows服务
├── Projects/                 # 客户定制项目
├── docs/                     # 文档资源（VitePress站点）
└── Scripts/                  # 构建和自动化脚本
```

📖 [详细项目结构 →](docs/05-resources/project-structure/README.md) | [模块文档对照 →](docs/05-resources/project-structure/module-documentation-map.md)

## 📚 文档导航

### 🚀 快速入门
- [入门指南](docs/00-getting-started/README.md) - 新手完整安装和使用指南
- [什么是 ColorVision](docs/00-getting-started/what-is-colorvision.md) - 产品介绍
- [快速开始](docs/00-getting-started/quick-start.md) - 快速上手教程
- [系统要求](docs/00-getting-started/prerequisites.md) - 环境要求和依赖
- [安装指南](docs/00-getting-started/installation.md) - 详细安装步骤
- [首次运行](docs/00-getting-started/first-steps.md) - 首次运行配置

### 📖 用户指南
- [界面使用](docs/01-user-guide/interface/main-window.md) - 主窗口导览
- [图像编辑器](docs/01-user-guide/image-editor/overview.md) - 图像编辑功能
- [设备管理](docs/01-user-guide/devices/overview.md) - 设备服务和集成
- [工作流程](docs/01-user-guide/workflow/README.md) - 工作流程概览与设计
- [数据管理](docs/01-user-guide/data-management/README.md) - 数据管理和导出导入
- [故障排查](docs/01-user-guide/troubleshooting/common-issues.md) - 常见问题和解决方案

### 🏗️ 架构与组件
- [系统架构概览](docs/00-getting-started/introduction/system-architecture.md) - 系统整体架构
- [架构设计详解](docs/03-architecture/README.md) - 详细架构文档
- [项目结构](docs/05-resources/project-structure/README.md) - 目录结构和模块说明
- [UI 组件 API](docs/04-api-reference/ui-components/README.md) - UI层组件文档
- [Engine 组件 API](docs/04-api-reference/engine-components/README.md) - Engine层组件文档

### 👨‍💻 开发指南
- [插件开发](docs/02-developer-guide/plugin-development/overview.md) - 插件开发指南
- [UI 开发](docs/02-developer-guide/ui-development/README.md) - UI开发概览与MVVM
- [Engine 开发](docs/02-developer-guide/engine-development/README.md) - Engine开发与服务
- [扩展性概览](docs/02-developer-guide/core-concepts/extensibility.md) - 扩展接口和自定义组件
- [性能优化](docs/02-developer-guide/performance/overview.md) - 性能优化指南

### 📚 API 参考
- [算法 API](docs/04-api-reference/algorithms/README.md) - 算法接口文档
- [流程引擎](docs/04-api-reference/algorithms/templates/flow-engine.md) - 可视化流程编辑
- [模板系统](docs/04-api-reference/algorithms/templates/template-management.md) - 模板管理与使用
- [插件 API](docs/04-api-reference/plugins/standard-plugins/pattern.md) - 标准插件参考

### 🚀 部署运维
- [部署文档](docs/02-developer-guide/deployment/overview.md) - 部署和配置
- [自动更新](docs/02-developer-guide/deployment/auto-update.md) - 更新系统说明
- [数据存储](docs/05-resources/data-storage.md) - 数据库和持久化

**🌐 在线文档站点**: https://xincheng213618.github.io/scgd_general_wpf/

## 🔧 技术栈

- **主框架**: .NET 8.0, WPF
- **平台**: Windows x64/ARM64
- **UI库**: HandyControl, WPF Extended Toolkit
- **数据库**: MySQL, SQLite
- **通信**: MQTT (MQTTnet), Socket
- **图像处理**: OpenCvSharp4, OpenCV (C++)
- **任务调度**: Quartz.NET
- **日志**: log4net
- **测试**: xUnit

## 🤝 贡献

欢迎贡献代码、报告问题或提出建议！

- 查看 [贡献指南](CONTRIBUTING.md)
- 提交 [Issue](https://github.com/xincheng213618/scgd_general_wpf/issues)
- 创建 [Pull Request](https://github.com/xincheng213618/scgd_general_wpf/pulls)

## 📝 更新日志

查看 [CHANGELOG.md](CHANGELOG.md) 了解版本更新历史和新功能。

## 📄 许可证

本项目采用 **MIT 许可证**，允许自由使用、修改和分发。详见 [LICENSE](LICENSE) 文件。

## 🙏 致谢

感谢以下开源项目：
- [Quartz.NET](https://www.quartz-scheduler.net/) - 企业级任务调度
- [HandyControl](https://github.com/HandyOrg/HandyControl) - 现代化WPF控件库
- [log4net](https://logging.apache.org/log4net/) - .NET日志框架
- [MQTTnet](https://github.com/dotnet/MQTTnet) - MQTT通信协议
- [OpenCvSharp](https://github.com/shimat/opencvsharp) - OpenCV .NET封装

感谢所有为 ColorVision 项目贡献代码、文档和建议的开发者和用户！
