# 安装 ColorVision


# 安装 ColorVision

## 目录
1. [介绍](#介绍)
2. [项目结构](#项目结构)
3. [核心组件](#核心组件)
4. [架构概览](#架构概览)
5. [详细组件分析](#详细组件分析)
6. [依赖分析](#依赖分析)
7. [性能考虑](#性能考虑)
8. [故障排除指南](#故障排除指南)
9. [总结](#总结)
10. [附录](#附录)

## 介绍
ColorVision 是一款专业的视觉检测软件，支持多种图像处理和分析功能。本文档主要针对 ColorVision 的安装过程进行详细说明，涵盖如何获取安装包、安装选项（特别是基于 Advanced Installer 生成的安装包），以及安装后的验证步骤和必要的手动配置说明，帮助用户顺利完成软件安装并开始使用。

---

## 项目结构

### 1. ColorVisionSetup
- 该目录包含 ColorVision 安装程序相关的代码和配置文件。
- 关键文件：
  - `ColorVisionSetup.csproj`：该项目文件定义了安装程序的构建配置、依赖项、资源文件和安装行为。
  - 相关源码文件如 `Core.cs`、`AutoUpdater.cs`、`WindowUpdate.xaml` 等，负责安装界面和自动更新功能。
- 说明：该模块负责生成 ColorVision 的安装包，支持安装过程中的界面显示、文件复制、注册表写入及自动更新机制。

### 2. Advanced
- 包含 Advanced Installer 工具生成的安装包项目文件 `ColorVision.aip`。
- 该文件详细描述了安装包的结构、组件、文件映射、安装条件、界面元素、注册表项和自定义操作。
- 说明：这是 ColorVision 安装包的核心配置文件，定义了安装程序的所有逻辑和资源。

### 3. ColorVision 及相关子项目
- `ColorVision` 目录包含主程序代码，包含 UI、插件、更新模块、项目管理等子模块。
- 其他子项目如 `ColorVision.Engine`、`ColorVision.UI`、`ColorVision.Common` 等，分别负责软件核心引擎、界面组件和通用工具库。
- 说明：主程序代码组织清晰，采用模块化设计，便于维护和扩展。

### 4. Plugins
- 包含多个插件模块，如 `EventVWR`、`ScreenRecorder`、`WindowsServicePlugin` 等。
- 说明：插件机制支持功能扩展，安装时会将插件程序集一并部署。

### 5. Scripts
- 包含安装相关的批处理和 PowerShell 脚本，如 `Uninstall.ps1`、`Modify.bat` 等。
- 说明：辅助安装和卸载过程，支持自定义配置和维护。

### 6. DLL 和 Engine 目录
- 存放底层功能库和引擎模块，包含图像处理、相机控制、算法实现等。
- 说明：实现软件的关键功能，通常作为动态链接库被主程序调用。

### 7. Docs
- 存放文档文件，包括许可协议、API 文档、解决方案说明等。
- 说明：提供软件使用、开发和许可相关的说明。

### 8. 其他资源目录
- 包含图像资源、主题、语言文件等，如 `UI/ColorVision.Themes`。
- 说明：支持软件界面美化和多语言功能。

---

## 核心组件

### ColorVisionSetup.csproj（安装项目文件）
- 定义了安装项目的属性，如目标框架 .NET Framework 4.8、输出类型为 Windows 应用程序。
- 配置了安装包的发布路径、安装方式（磁盘安装）、是否启用自动更新等。
- 引用了多个系统库和 WPF 相关程序集，支持安装界面和功能实现。
- 包含安装界面定义文件（XAML），如 `CyclingGradient.xaml`、`WindowUpdate.xaml`。
- 嵌入了安装所需的图标和资源文件。
- 通过签名文件 `ColorVision.snk` 对安装程序集进行签名，保证安全性。
- 支持多平台配置（AnyCPU、x64），并针对不同配置设置不同的编译选项。

### ColorVision.aip（Advanced Installer 项目文件）
- 详细定义了安装包的组件、文件映射、目录结构和注册表项。
- 指定安装路径、快捷方式创建、安装条件（如操作系统版本、.NET Framework 版本）。
- 包含安装界面元素的定义，如欢迎界面、许可协议、安装进度等。
- 定义了安装过程中的自定义操作，如文件提取、软件检测、日志记录。
- 支持多语言界面，包含中文、英文、法文、日文、韩文等资源。
- 预设了安装包的升级策略，支持版本检测和覆盖安装。
- 设定了安装所需的先决条件（Prerequisites），如 ASP.NET Core 8.0.5 x64。

---

## 架构概览

ColorVision 的安装架构主要由以下部分组成：

- **安装包生成器（Advanced Installer）**：通过 `.aip` 文件配置安装逻辑，定义文件复制、注册表写入、快捷方式创建和安装界面。
- **安装程序项目（ColorVisionSetup）**：基于 WPF 实现的安装界面和自动更新功能，支持用户交互和安装过程控制。
- **主程序及插件部署**：安装包将主程序及多个插件文件复制到目标目录，确保软件完整运行。
- **系统环境检测**：安装过程中检测操作系统版本和 .NET Framework 版本，保证软件兼容性。
- **自动更新机制**：安装程序内置自动更新功能，支持版本检测和后台更新。
- **多语言支持**：安装界面和软件支持多语言资源，提升用户体验。

---

## 详细组件分析

### 1. ColorVisionSetup.csproj

- **作用**：定义安装程序的构建和发布配置。
- **关键配置**：
  - 目标框架：.NET Framework 4.8
  - 输出类型：Windows 可执行程序（WinExe）
  - 签名：使用 `ColorVision.snk` 进行强名称签名
  - 资源文件：图标、背景图片、界面 XAML 文件等
  - 编译选项：支持 Debug 和 Release 两种配置，分别针对 AnyCPU 和 x64 平台
  - 引用程序集：WPF 相关组件（PresentationCore、PresentationFramework 等）、系统库（System.Data、System.Xml 等）

- **主要源码文件**：
  - `App.xaml` 和 `App.xaml.cs`：定义应用程序入口和全局资源
  - `Core.cs`：安装核心逻辑
  - `AutoUpdater.cs`：自动更新实现
  - `WindowUpdate.xaml` 和 `WindowUpdate.xaml.cs`：更新界面
  - `CyclingGradient.xaml` 和 `CyclingGradient.xaml.cs`：安装界面动画效果
  - `Utils.cs`、`VideoPlayer.cs`、`ViewModelBase.cs`：辅助功能和基础类

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  <AssemblyOriginatorKeyFile>$(SolutionDir)ColorVision.snk</AssemblyOriginatorKeyFile>
  <PublishUrl>publish\</PublishUrl>
  <Install>true</Install>
  <InstallFrom>Disk</InstallFrom>
  <BootstrapperEnabled>true</BootstrapperEnabled>
</PropertyGroup>
```

### 2. ColorVision.aip

- **作用**：Advanced Installer 项目文件，定义安装包的详细配置。
- **主要内容**：
  - 组件定义：列出所有文件、DLL、资源文件及其安装路径。
  - 目录结构：定义安装目录及子目录，如 `APPDIR`、`Plugins_Dir`、`Projects_Dir` 等。
  - 安装界面：定义安装过程中显示的对话框、按钮、文本框等控件。
  - 注册表操作：写入软件注册信息，关联文件类型和快捷方式。
  - 安装条件：限制安装环境，检查系统版本和 .NET Framework 版本。
  - 自定义操作：安装过程中的脚本和程序调用。
  - 升级策略：支持版本升级检测和旧版本卸载。
  - 先决条件：自动检测和安装必备组件，如 ASP.NET Core 运行时。

```xml
<ROW Feature="MainFeature" Component_="ColorVision.exe"/>
<ROW Directory="APPDIR" Directory_Parent="TARGETDIR" DefaultDir="APPDIR:."></ROW>
<ROW Property="ProductName" Value="ColorVision"/>
<ROW Condition="AI_DETECTED_DOTNET_VERSION &gt;= AI_REQUIRED_DOTNET_VERSION" Description="[ProductName] 无法安装在 .NET Framework 版本低于 [AI_REQUIRED_DOTNET_DISPLAY] 的系统上。"/>
```

---

## 依赖分析

- 安装程序依赖多种第三方库和系统组件，如：
  - WPF 相关组件支持界面显示
  - OpenCV 相关 DLL 提供图像处理功能
  - MQTT、MySQL 等库支持设备通信和数据存储
  - 日志库 log4net 用于日志记录
  - 压缩库 LZ4、SharpZipLib 用于文件压缩处理
- 安装包通过 Advanced Installer 管理所有文件和依赖，确保安装完整性。
- 注册表写入确保文件类型关联和快捷方式创建。
- 先决条件确保系统满足软件运行环境要求。

---

## 性能考虑

- 安装程序采用异步文件复制和界面动画，提升用户体验。
- 自动更新功能支持后台下载和差异更新，减少网络负载。
- 组件化设计便于按需加载和维护，避免冗余。
- 使用签名和校验机制保证安装包安全性和完整性。

---

## 故障排除指南

- 检查操作系统版本和 .NET Framework 是否满足安装要求。
- 确认安装包完整无损，避免下载或传输过程损坏。
- 安装过程中遇到权限问题，尝试以管理员身份运行安装程序。
- 查看安装日志文件，定位具体错误信息。
- 卸载旧版本后重新安装，避免版本冲突。
- 如遇自动更新失败，手动下载最新安装包重新安装。

---

## 总结

本文档详细介绍了 ColorVision 的安装流程，重点分析了安装项目文件 `ColorVisionSetup.csproj` 和 Advanced Installer 项目文件 `ColorVision.aip`，说明了安装包的构建、文件组织、安装界面设计和依赖管理。通过多语言支持、自动更新和先决条件检测，确保安装过程顺利且用户体验良好。用户可根据本文档指导完成 ColorVision 的安装和验证，快速投入使用。

---

## 附录

### 相关资源链接
- 安装项目文件源代码：[GitHub - ColorVisionSetup.csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/main/ColorVisionSetup/ColorVisionSetup.csproj)
- 安装包配置文件：[GitHub - ColorVision.aip](https://github.com/xincheng213618/scgd_general_wpf/blob/main/Advanced/ColorVision.aip)

