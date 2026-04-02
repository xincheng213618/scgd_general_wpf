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
dotnet build

# 运行主程序
dotnet run --project ColorVision/ColorVision.csproj
```

## 📁 项目结构

```
ColorVision/
├── ColorVision/              # 主程序入口
├── Engine/                   # 核心引擎层
│   ├── ColorVision.Engine/   # 主引擎：流程管理、设备服务、模板系统 (v1.4.2.1)
│   ├── cvColorVision/        # 视觉处理核心：相机控制、色彩算法 (v2025.8.9.0)
│   ├── FlowEngineLib/        # 流程引擎库：可视化节点编辑 (v1.6.1)
│   ├── ColorVision.FileIO/   # 文件IO处理：CVRaw/CVCIE格式 (v1.3.12.24)
│   └── ST.Library.UI/        # 节点编辑器UI库 (v1.2.0.2410)
├── UI/                       # 用户界面层
│   ├── ColorVision.UI/       # 主UI框架：插件系统、菜单、快捷键
│   ├── ColorVision.Themes/   # 主题管理：五主题支持
│   ├── ColorVision.ImageEditor/  # 图像编辑器：结果叠加显示
│   ├── ColorVision.UI.Desktop/   # 桌面应用入口
│   └── ColorVision.Common/   # 通用基础库：MVVM、接口定义
├── Plugins/                  # 扩展插件
│   ├── EventVWR/            # 事件查看器与Dump管理
│   ├── Spectrum/            # 光谱仪测试工具 (v2.1.4.0)
│   ├── SystemMonitor/       # 系统性能监控
│   ├── Pattern/             # 图卡生成工具
│   ├── ScreenRecorder/      # 屏幕录制工具
│   ├── ImageProjector/      # 图片投影工具
│   └── WindowsServicePlugin/ # Windows服务管理
├── Projects/                 # 客户定制项目
├── docs/                     # 文档资源（VitePress站点）
└── Scripts/                  # 构建和自动化脚本
```

## 📚 文档导航

### 🚀 快速入门
- [入门指南](docs/00-getting-started/README.md) - 新手完整安装和使用指南
- [系统要求](docs/00-getting-started/prerequisites.md) - 环境要求和依赖
- [安装指南](docs/00-getting-started/installation.md) - 详细安装步骤

### 📖 用户指南
- [界面使用](docs/01-user-guide/interface/main-window.md) - 主窗口导览
- [设备管理](docs/01-user-guide/devices/overview.md) - 设备服务和集成
- [工作流程](docs/01-user-guide/workflow/README.md) - 工作流程概览
- [故障排查](docs/01-user-guide/troubleshooting/common-issues.md) - 常见问题和解决方案

### 🏗️ 开发者指南
- [插件开发](docs/02-developer-guide/plugin-development/overview.md) - 插件开发指南
- [UI 开发](docs/02-developer-guide/ui-development/README.md) - UI开发概览与MVVM
- [流程引擎](docs/02-developer-guide/algorithm-engine-templates/flow-engine/流程引擎.md) - 流程引擎开发
- [模板系统](docs/02-developer-guide/algorithm-engine-templates/template-management/模板管理.md) - 模板开发

### 📚 API 参考
- [UI 组件](docs/04-api-reference/ui-components/README.md) - UI层组件文档
- [Engine 组件](docs/04-api-reference/engine-components/README.md) - Engine层组件文档
- [插件 API](docs/04-api-reference/plugins/) - 标准插件参考

### 🚀 部署运维
- [部署文档](docs/02-developer-guide/deployment/overview.md) - 部署和配置
- [自动更新](docs/02-developer-guide/deployment/auto-update.md) - 更新系统说明

**🌐 在线文档站点**: https://xincheng213618.github.io/scgd_general_wpf/

## 🔧 技术栈

| 类别 | 技术 |
|------|------|
| **主框架** | .NET 8.0 / .NET 10.0, WPF |
| **平台** | Windows x64 / ARM64 |
| **UI库** | HandyControl 3.5.1, WPF-UI 4.2.0 |
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

# 构建插件
py Scripts/build_plugin.py -p PluginName

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
- [WPF-UI](https://github.com/lepoco/wpfui) - WPF现代化UI框架
- [OpenCvSharp](https://github.com/shimat/opencvsharp) - OpenCV .NET封装
- [MQTTnet](https://github.com/dotnet/MQTTnet) - MQTT通信协议
- [Quartz.NET](https://www.quartz-scheduler.net/) - 企业级任务调度
- [ScottPlot](https://scottplot.net/) - .NET图表库
- [log4net](https://logging.apache.org/log4net/) - .NET日志框架

---

*最后更新: 2026-04-02*  
*ColorVision 开发团队*
