# 系统架构概览

本页不再尝试用一套抽象分层把整个仓库讲成标准教材，而是直接按当前代码仓库的主目录说明系统怎么被组织起来，以及读代码时通常从哪里切入。

## 先怎么理解这个仓库

从当前目录结构看，ColorVision 更接近一套以桌面主程序为核心、围绕引擎、UI、插件、项目包和安装更新体系展开的 Windows WPF 平台。

最重要的几个顶层区域是：

- `ColorVision/`：主应用入口和主窗口
- `UI/`：WPF UI 框架、主题、属性编辑器、图像编辑器、数据库与桌面菜单等
- `Engine/`：设备服务、模板系统、流程执行、OpenCV 集成、文件处理
- `Plugins/`：运行时插件扩展
- `Projects/`：客户项目和定制业务组合
- `ColorVisionSetup/`：安装器与更新相关程序
- `Web/Backend/`：插件市场后端
- `Scripts/`：构建、打包、发布脚本

## 按系统角色看结构

### 主程序层

`ColorVision/` 是桌面应用入口，负责主窗口、应用启动、全局配置、更新入口和整体工作台组织。

如果你在追“程序启动后先发生什么”，通常从这里开始，再联动看 `UI/` 和 `Engine/`。

### UI 层

`UI/` 不是单一项目，而是一组界面相关模块的集合。当前比较关键的包括：

- `ColorVision.UI/`：通用 UI 框架和菜单、面板、属性编辑器等能力
- `ColorVision.Themes/`：主题和视觉资源
- `ColorVision.ImageEditor/`：图像查看、标注和结果展示
- `ColorVision.Database/`：数据库浏览器等数据库相关 UI 能力
- `ColorVision.UI.Desktop/`：桌面级菜单和设置入口

### 引擎层

`Engine/` 是系统的业务核心，但也不是一个单项目名字空间。当前主要由几块组成：

- `ColorVision.Engine/`：设备服务、模板系统、流程窗口、MQTT 与业务协调
- `FlowEngineLib/`：流程节点编辑与执行底座
- `cvColorVision/`：底层视觉处理与 OpenCV 相关集成
- `ColorVision.FileIO/`：文件读写处理
- `ColorVision.ShellExtension/`：外部集成相关扩展

### 插件与项目层

- `Plugins/` 提供运行时插件扩展，例如 Conoscope、Spectrum、SystemMonitor 等
- `Projects/` 放客户项目或业务打包实现，通常是把现有引擎与 UI 能力重新组合成特定方案

### 交付与外围层

- `ColorVisionSetup/` 负责安装与更新侧程序
- `Web/Backend/` 负责插件市场后端
- `Scripts/` 和根目录批处理脚本负责构建、打包和发布

## 运行时最常见的主链路

如果从用户操作一路往下看，最常见的链路通常是：

1. 用户从 `ColorVision/` 的主窗口进入某个功能。
2. `UI/` 中对应窗口或面板负责展示与交互。
3. `Engine/ColorVision.Engine/` 中的设备服务、模板或流程逻辑接手业务处理。
4. 需要流程执行时进一步调用 `Engine/FlowEngineLib/`。
5. 需要图像或算法处理时继续联动 `Engine/cvColorVision/`、`UI/ColorVision.ImageEditor/` 或具体模板实现。
6. 如果功能来自外部扩展，再进入 `Plugins/` 或 `Projects/` 中的实现。

## 读代码时的常见切入点

### 想理解主界面和入口

先看：

- `ColorVision/`
- `UI/ColorVision.UI/`
- `UI/ColorVision.UI.Desktop/`

### 想理解设备、模板和流程

先看：

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Templates/`
- `Engine/FlowEngineLib/`

### 想理解图像结果和显示

先看：

- `UI/ColorVision.ImageEditor/`
- `Engine/cvColorVision/`

### 想理解扩展能力

先看：

- `Plugins/`
- `Projects/`
- [插件开发概览](../../02-developer-guide/plugin-development/overview.md)

## 这页不再做什么

本页不再继续维护这些容易失真的内容：

- 虚构的标准化六层架构命名
- 与当前目录不一致的模块名清单
- 泛化的“依赖注入容器”“对象池”“报告模板”等教材式概括

如果某个专题需要更细的运行时关系、流程执行链或模板结构，应进入对应专题页说明，而不是在这里一次讲完。

## 继续阅读

- [架构运行时](./runtime.md)
- [组件交互](./component-interactions.md)
- [FlowEngineLib 架构](../components/engine/flow-engine.md)
- [Templates 架构设计](../components/templates/design.md)

## 说明

- 本页只作为当前仓库结构下的系统入口图，不再继续维护脱离代码目录的抽象分层稿。
