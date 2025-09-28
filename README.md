# ColorVision

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/xincheng213618/scgd_general_wpf)

## 项目简介

ColorVision 是一款基于 WPF 的专业视觉检测平台，采用模块化架构设计，支持多框架协同工作。专注于提供高效、精准的图像处理及分析功能，适用于光电技术、色彩管理、质量检测等应用场景。

## 📖 在线文档 / GitHub Pages

- **中文文档**: https://xincheng213618.github.io/scgd_general_wpf/

## 🚀 快速入口

- [入门指南](docs/getting-started/入门指南.md) - 新用户完整安装和使用指南
- [更新系统](docs/update/README.md) - 自动更新机制和版本管理

## ✨ 主要特性

- **🎨 多主题支持** - 深色、浅色、粉色、青色主题，支持跟随系统
- **🌐 多语言国际化** - English、简体中文、繁体中文、日本語、한국어，支持跟随系统
- **🔌 灵活插件机制** - 支持插件热加载和扩展功能开发
- **⚡ 流程引擎** - 可视化流程编辑器，支持算法模板和服务配置
- **📷 设备集成** - 支持多种相机、光谱仪、传感器等设备
- **📊 数据库持久化** - MySQL/SQLite 双数据库支持，完整数据管理
- **🔄 自动更新系统** - 增量更新、签名验证、自动回滚机制
- **📁 解决方案管理** - 支持工程文件、图像预览、批处理操作
- **🌐 网络通信** - MQTT、Socket 多协议支持
- **⏰ 任务调度** - 支持定时任务、批量处理、Cron 表达式
- **🎯 图像编辑器** - 专业图像编辑工具，支持ROI绘制、图形标注
- **📈 性能监控** - 系统监控、日志管理、性能分析工具

## 📁 目录结构

```
ColorVision/
├── ColorVision/              # 主程序和插件管理
├── Engine/                   # 核心引擎模块
│   ├── ColorVision.Engine/   # 流程引擎和模板管理
│   ├── cvColorVision/        # 视觉处理核心代码
│   ├── FlowEngineLib/        # 流程引擎库
│   └── ColorVision.FileIO/   # 文件IO处理
├── UI/                       # 用户界面模块
│   ├── ColorVision.UI/       # 主界面框架
│   ├── ColorVision.Themes/   # 主题管理
│   ├── ColorVision.Common/   # 通用UI组件
│   └── ColorVision.*/        # 其他UI模块
├── Plugins/                  # 扩展插件
│   ├── EventVWR/            # 事件查看器
│   ├── SystemMonitor/       # 系统监控
│   └── WindowsServicePlugin/ # Windows服务插件
├── Projects/                 # 客户定制项目
├── docs/                     # 文档资源
└── Scripts/                  # 构建和自动化脚本
```

## 🚀 快速开始

### 环境要求

- **.NET 8** (推荐) 或 **.NET Framework 4.8**
- **Windows 10 1903+** 或 **Windows 11**
- **Visual Studio 2022** (开发环境)
- **分辨率**: 1920x1080 或更高，100% 缩放

### 构建命令

```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行主程序
dotnet run --project ColorVision/ColorVision.csproj
```

## 📚 文档结构说明

- `docs/getting-started/` - 入门指南和快速上手
- `docs/architecture/` - 系统架构和设计文档  
- `docs/ui-components/` - UI 组件使用说明
- `docs/engine-components/` - Engine 组件详细说明
- `docs/plugins/` - 插件开发和使用指南
- `docs/deployment/` - 部署和运维文档
- `docs/update/` - 更新系统说明

## 🔌 插件机制说明

ColorVision 采用基于接口的插件架构，支持动态加载和扩展：

- **插件目录**: `Plugins/` - 将插件文件夹放置于此目录
- **插件接口**: 实现 `IPlugin` 接口即可被自动发现
- **自动加载**: 程序启动时自动扫描并加载插件
- **标准插件**: EventVWR、SystemMonitor、Pattern 等

### 自定义插件放置方式
1. 在 `Plugins/` 目录下创建插件文件夹
2. 实现 `IPlugin` 接口
3. 编译后的插件程序集放入对应文件夹
4. 重启程序即可自动加载

## 🏗️ 子项目说明

- **Engine** - 核心业务逻辑层，包含视觉算法、设备通讯、数据处理
- **UI** - 用户界面层，提供主题管理、通用控件、界面框架
- **Projects** - 客户定制项目，针对特定需求的完整解决方案
- **Plugins** - 扩展插件层，提供额外功能和工具支持

## 📝 更新日志

详细的版本更新记录请查看 [CHANGELOG.md](CHANGELOG.md)

## 🤝 贡献指南

我们欢迎社区贡献！请遵循以下步骤：

1. **Fork** 本仓库到你的 GitHub 账户
2. **Branch** 创建功能分支: `git checkout -b feature/your-feature`
3. **Commit** 提交你的修改: `git commit -am 'Add some feature'`
4. **Push** 推送到分支: `git push origin feature/your-feature`
5. **Pull Request** 提交合并请求

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) 进行许可。详细信息请参阅 [LICENSE](docs/LICENSE.rtf) 文件。

## 🙏 致谢

感谢所有为 ColorVision 项目贡献代码和建议的开发者和用户。

---

**视彩（上海）光电技术有限公司**









