# ProjectStarkSemi (星钥半导体)

## 🎯 功能定位

星钥半导体客户定制项目 - 集成了锥光镜观察系统和MVS相机控制的专业光学测试解决方案

## 作用范围

为星钥半导体客户提供专业的光学测试系统，包括锥光镜（Conoscope）观察功能和MVS工业相机集成，支持定制化的语言菜单系统。

## 主要功能点

- **锥光镜观察系统** - 完整的锥光镜测试窗口（ConoscopeWindow）
  - 支持VA60和VA80两种硬件型号
  - VA60: 一台观察相机（视频模式）+ 一台测量相机（需要校正）
  - VA80: 一台测量相机（需要校正）
  - 图像滤波和处理功能
  - 实时视频显示和图像捕获
- **MVS相机集成** - 海康威视MVS工业相机支持（MVSViewWindow）
  - 相机设备枚举和连接
  - 实时视频流显示
  - 图像采集和处理
  - 相机参数配置
- **定制语言菜单** - 灵活的多语言切换功能
  - 动态语言菜单生成
  - 支持多语言实时切换
  - 快捷键支持（Ctrl + Shift + T）
- **模板和流程管理** - 基于ColorVision引擎的模板系统
- **设备服务集成** - 与ColorVision设备管理系统的深度集成
- **MQTT通信** - 支持MQTT消息通信协议

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 核心引擎功能（设备管理、模板、MQTT、数据库）
- ColorVision.UI - 基础UI组件和插件系统
- ColorVision.ImageEditor - 图像编辑和显示功能
- CVCommCore.dll - 通信核心组件

**被引用方式**:
- 作为独立应用程序运行
- 可通过插件系统加载到主程序
- 支持独立窗口模式

## 使用方式

### 引用方式
作为独立项目编译和部署

```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.ImageEditor\ColorVision.ImageEditor.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.UI\ColorVision.UI.csproj" />
```

### 启动和运行
- 直接运行编译后的可执行文件
- 自动加载插件系统（从Plugins目录）
- 启动时显示锥光镜观察窗口（ConoscopeWindow）
- 通过菜单"视图"可打开MVS视频窗口

### 主要功能模块
- **锥光镜测试** - 通过ConoscopeWindow进行光学测试
- **MVS相机控制** - 通过MVSViewWindow进行相机操作
- **语言切换** - 通过语言菜单切换系统语言
- **设备管理** - 相机设备的连接和参数配置

## 开发调试

```bash
# 构建项目
dotnet build Projects/ProjectStarkSemi/ProjectStarkSemi.csproj

# 运行项目（Windows only）
dotnet run --project Projects/ProjectStarkSemi/ProjectStarkSemi.csproj
```

## 目录说明

- `App.xaml/.cs` - 应用程序入口和初始化
- `ConoscopeWindow.xaml/.cs` - 锥光镜主测试窗口
- `MVSViewWindow.xaml/.cs` - MVS相机视频显示窗口  
- `MenuLanguage.cs` - 语言菜单插件实现
- `MVCamera.cs` - MVS相机SDK封装（海康威视）
- `manifest.json` - 插件清单文件
- `Properties/` - 项目属性和资源文件
- `Images/` - 图像资源文件

## 配置说明

### 硬件型号配置
- **VA60模式**: 双相机配置（观察相机 + 测量相机）
- **VA80模式**: 单测量相机配置
- 支持相机自动枚举和手动选择

### 语言配置
- 支持多语言动态切换
- 语言配置保存在LanguageConfig中
- 通过LanguageManager管理语言资源

### 插件配置
- 插件版本: 1.0
- 最低主程序要求: 1.3.14.66
- 自动复制到主程序Plugins目录

### 数据库和MQTT配置
- 自动连接MySQL数据库
- 支持MQTT消息通信
- 服务和设备管理集成

## 技术特性

### 相机支持
- 海康威视MVS工业相机SDK集成
- 支持USB、GigE等多种接口相机
- 实时视频流显示和图像采集
- 相机参数可配置

### 图像处理
- 基于OpenCvSharp的图像处理
- 支持多种图像滤波算法
- 实时图像显示和绘制功能

### 系统集成
- 完整的设备服务架构
- 模板和流程引擎支持
- 授权和日志系统
- 主题系统支持

## 相关文档链接

- [插件开发指南](../../docs/02-developer-guide/plugin-development/overview.md)
- [项目架构文档](../../docs/03-architecture/README.md)
- [引擎开发文档](../../docs/02-developer-guide/engine-development/README.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)

## 维护者

ColorVision 项目团队

## 版本历史

参见 [CHANGELOG.md](CHANGELOG.md)
