# ColorVision

[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://learn.microsoft.com/windows/)
[![UI](https://img.shields.io/badge/UI-WPF-blue.svg)](https://learn.microsoft.com/dotnet/desktop/wpf/)

ColorVision 是一个 Windows WPF 视觉检测平台，面向光电检测、设备集成、可视化流程执行、图像分析、插件扩展和客户项目交付。

维护中的完整文档以简体中文为准：

- [在线文档](https://xincheng213618.github.io/scgd_general_wpf/)
- [本地文档入口](docs/index.md)

## 仓库结构

| 路径 | 作用 |
| --- | --- |
| `ColorVision/` | 主 WPF 应用入口 |
| `UI/` | UI 类库、主题、图像编辑器、数据库 UI、调度、Socket 协议和桌面基础设施 |
| `Engine/` | 设备服务、模板系统、FlowEngine 集成、MQTT、数据访问、结果处理和文件 I/O |
| `Native/` | C++ OpenCV helper 和导出的 native 算法 |
| `Plugins/` | 运行时发现的通用插件 |
| `Projects/` | 客户或场景定制项目包 |
| `Scripts/` | 构建、打包、发布和后端辅助脚本 |
| `Test/` | xUnit 和 native helper 测试 |
| `Web/` | 插件市场后端和相关 Web 模块 |
| `docs/` | VitePress 文档源码 |

更完整的目录说明见 [项目结构总览](docs/05-resources/project-structure/README.md)。

## 环境要求

- Windows 10/11
- Visual Studio 2022 或 MSBuild
- 当前主线使用 .NET 10 SDK
- 文档站需要 Node.js 20+
- 发布、打包和后端脚本需要 Python 3.9+
- 常规桌面交付以 x64 为主

运行时依赖的 vendor/native 文件以安装和交付文档为准。

## 构建

```powershell
dotnet restore
dotnet build build.sln -p:Platform=x64
```

单独构建和运行主程序：

```powershell
dotnet build ColorVision/ColorVision.csproj -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj -p:Platform=x64
```

GitHub Actions 的 Windows 构建使用 MSBuild：

```powershell
msbuild build.sln /p:Configuration=Release /p:Platform=x64
```

## 测试

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64
```

后端测试：

```powershell
cd Web/Backend
python test_app.py
python test_app_releases.py
```

更多验证范围见 [测试与验证](docs/02-developer-guide/testing.md)。

## 文档

本仓库按标准 Git 工程方式组织文档入口：

- `README.md`：仓库总览和第一组命令
- `CONTRIBUTING.md`：开发、测试、文档和发布贡献规则
- `CHANGELOG.md`：版本历史
- `LICENSE.md`：许可入口
- `docs/`：用户、开发、项目、插件、交付和源码参考的详细文档

构建文档站：

```powershell
npm install
npm run docs:build
```

`docs:build` 会构建站点、生成自定义索引，并校验内部链接、旧页面兼容入口和搜索索引。

已有构建产物时可单独复查：

```powershell
npm run docs:validate
```

启动本地文档站：

```powershell
npm run docs:dev
```

文档语言策略：

- 简体中文是完整文档和事实来源。
- 英文、繁体中文、日文、韩文历史副本已从当前工作树移除；如确有交付需要，可从 Git 历史找回后重新维护。

## 打包与发布

常规发布入口：

```powershell
Scripts\release.bat
```

不要为主安装包新增或使用本地-only 发布捷径。发布脚本链负责构建、打包、上传发布产物、更新远端发布元数据和生成更新包。

本地打包插件或客户项目：

```powershell
Scripts\package_plugin.bat Spectrum
Scripts\package_project.bat ProjectLUX
```

更多说明：

- [构建与发布脚本](docs/02-developer-guide/scripts/README.md)
- [部署概览](docs/02-developer-guide/deployment/overview.md)
- [插件开发](docs/02-developer-guide/plugin-development/README.md)
- [项目说明](docs/00-projects/README.md)

## 参与开发

提交 PR 前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。保持变更聚焦，运行相关验证，并更新与行为变化对应的文档入口。

## 许可

见 [LICENSE.md](LICENSE.md) 和维护中的 [软件许可协议](docs/05-resources/legal/software-agreement.md)。
