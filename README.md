# ColorVision

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/xincheng213618/scgd_general_wpf)
![.NET Version](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)
![WPF](https://img.shields.io/badge/UI-WPF-blue.svg)
![License](https://img.shields.io/github/license/xincheng213618/scgd_general_wpf.svg)

## 📋 项目简介

ColorVision 是一款基于 WPF 的专业视觉检测平台，采用模块化架构设计，支持插件化扩展。专注于提供高效、精准的图像处理及分析功能，适用于光电技术、色彩管理、质量检测等应用场景。

**📚 完整文档**: https://xincheng213618.github.io/scgd_general_wpf/

## ✨ 核心特性

- **🎨 多主题支持** - 深色、浅色、粉色、青色、系统跟随五种主题
- **🌐 多语言国际化** - English、简体中文、繁体中文、日本語、한국어、Français、Русский
- **🔌 插件化架构** - 运行时动态加载插件，支持自定义扩展
- **⚡ 流程引擎** - 可视化流程编辑器，支持拖拽式节点编排
- **📷 设备集成** - 支持相机、光谱仪、电机、传感器等多种设备
- **🔄 自动更新** - 增量更新、版本检测、自动下载安装
- **📊 数据分析** - 实时数据可视化、报告生成、导出功能

## 🚀 快速开始

### 环境要求

- **.NET 8.0 SDK** 或 **.NET 10.0 SDK**
- **Windows 10 1903+** 或 **Windows 11**
- **Visual Studio 2022+** (推荐)

### 构建与运行

```bash
# 克隆仓库
git clone https://github.com/xincheng213618/scgd_general_wpf.git

# 恢复依赖
dotnet restore

# 构建项目
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64

# 运行主程序
dotnet run --project .\ColorVision\ColorVision.csproj -p:Platform=x64
```

## 📁 项目结构

```
ColorVision/
├── ColorVision/              # 主程序入口与桌面壳层
├── Engine/                   # 核心引擎、模板系统、流程引擎、原生互操作
├── UI/                       # WPF UI 框架、主题、图像编辑器、Socket 通信与桌面基础设施
├── Plugins/                  # 运行时插件（当前源码主体含 EventVWR、Spectrum、SystemMonitor、WindowsServicePlugin 等）
├── Projects/                 # 客户或场景定制项目包
├── ColorVisionSetup/         # 安装器与更新器相关项目
├── docs/                     # VitePress 文档站
└── Scripts/                  # 构建、打包、发布脚本
```

## 📚 文档导航

### 安装与首次使用

- [入门指南](docs/00-getting-started/README.md) - 安装、首次运行与最短上手路径
- [系统要求](docs/00-getting-started/prerequisites.md) - 运行环境与源码构建前提
- [安装指南](docs/00-getting-started/installation.md) - 安装包安装与源码运行

### 日常使用

- [用户指南](docs/01-user-guide/README.md) - 面向日常操作的总入口
- [主窗口导览](docs/01-user-guide/interface/main-window.md) - 熟悉主界面与基础交互
- [设备服务概览](docs/01-user-guide/devices/overview.md) - 设备接入和管理入口
- [工作流程概览](docs/01-user-guide/workflow/README.md) - 流程设计与执行入口

### 开发与交付

- [开发指南](docs/02-developer-guide/README.md) - 二次开发与交付链路总览
- [扩展性概览](docs/02-developer-guide/core-concepts/extensibility.md) - 扩展点与插件入口
- [Engine 开发指南](docs/02-developer-guide/engine-development/README.md) - Engine 与模板相关开发
- [插件开发总览](docs/02-developer-guide/plugin-development/README.md) - 插件开发入口
- [部署概览](docs/02-developer-guide/deployment/overview.md) - 安装器、更新与发布链路
- [Socket 通信优化路线](docs/02-developer-guide/performance/socket-protocol-optimization-roadmap.md) - TCP 服务、消息历史和管理窗口的后续优化路径

### 设计与源码导读

- [架构设计](docs/03-architecture/README.md) - 系统设计边界与运行时关系
- [API 参考](docs/04-api-reference/README.md) - 模块、接口与实现入口
- [ColorVision.SocketProtocol](docs/04-api-reference/ui-components/ColorVision.SocketProtocol.md) - 本地 TCP 服务、JSON/Text 分发、消息历史和管理窗口
- [项目结构总览](docs/05-resources/project-structure/README.md) - 仓库目录与文档映射   

**🌐 在线文档站点**: https://xincheng213618.github.io/scgd_general_wpf/

## 🔧 技术栈

| 类别 | 技术 |
|------|------|
| **主框架** | .NET 8.0 / .NET 10.0, WPF |
| **平台** | Windows x64 / ARM64 |
| **UI库** | HandyControl 3.5.1 |
| **数据库** | MySQL, SQLite (SqlSugar ORM) |
| **通信** | MQTT (MQTTnet 5.x), TCP/IP, 串口 |
| **图像处理** | OpenCvSharp4, OpenCV 4.x (C++) |
| **任务调度** | Quartz.NET |
| **图表** | ScottPlot |
| **日志** | log4net |

## 🛠️ 构建脚本

```bash
# 构建主程序
py Scripts/build.py

# 构建更新包
py Scripts/build_update.py

# 构建插件/项目包
Scripts/package_plugin.bat Pattern --no-upload
Scripts/package_project.bat ProjectARVR --no-upload

# 发布插件到市场
py Scripts/publish_plugin.py -p PluginId -v 1.0.0.1
```

## 🤝 贡献

欢迎贡献代码、报告问题或提出建议！

- 提交 [Issue](https://github.com/xincheng213618/scgd_general_wpf/issues)
- 创建 [Pull Request](https://github.com/xincheng213618/scgd_general_wpf/pulls)

## 📝 更新日志

查看 [CHANGELOG.md](CHANGELOG.md) 了解版本更新历史。

## 📄 许可证

本项目采用 **MIT 许可证**，详见 [LICENSE](LICENSE) 文件。

## 🙏 致谢

感谢以下开源项目：
- [HandyControl](https://github.com/HandyOrg/HandyControl) - 现代化WPF控件库
- [OpenCvSharp](https://github.com/shimat/opencvsharp) - OpenCV .NET封装
- [MQTTnet](https://github.com/dotnet/MQTTnet) - MQTT通信协议
- [Quartz.NET](https://www.quartz-scheduler.net/) - 企业级任务调度
- [ScottPlot](https://scottplot.net/) - .NET图表库
- [log4net](https://logging.apache.org/log4net/) - .NET日志框架

---

*最后更新: 2026-05-11*
*ColorVision 开发团队*
