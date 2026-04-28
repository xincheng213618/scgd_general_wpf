# ProjectStarkSemi (星钥半导体)

## 功能定位

星钥半导体客户定制项目 - 集成了锥光镜观察系统和MVS相机控制的专业光学测试解决方案

## 作用范围

为星钥半导体客户提供专业的光学测试系统，包括锥光镜（Conoscope）观察功能和MVS工业相机集成，支持多语言切换。

## 主要功能点

- **锥光镜观察系统** - 完整的锥光镜测试窗口（ConoscopeWindow）
  - 支持VA60和VA80两种硬件型号
  - VA60: 一台观察相机（视频模式）+ 一台测量相机（需要校正）
  - VA80: 一台测量相机（需要校正）
  - 图像滤波和处理功能
  - 实时视频显示和图像捕获
  - 方位角/极角分布曲线分析
  - 参考线与同心圆辅助分析工具
  - 基于AvalonDock的可拖拽面板布局系统
- **MVS相机集成** - 海康威视MVS工业相机支持（MVSViewWindow）
  - 相机设备枚举和连接
  - 实时视频流显示
  - 图像采集和处理
  - 相机参数配置
- **多语言支持** - 通过ColorVision.UI语言系统实现
  - 支持7种语言（中文、英文、法文、日文、韩文、俄文、繁体中文）
  - 通过LanguageConfig管理语言资源
- **数据导出** - 多模式数据导出
  - 方位角模式导出（Azimuth export）
  - 极角/同心圆模式导出（Polar export）
  - 截线导出（Cross-section export）
  - 高级导出弹窗（AdvancedExportDialog）
  - 支持X/Y/Z/CieX/CieY/CieU/CieV通道导出
- **模板和流程管理** - 基于ColorVision引擎的模板系统
- **设备服务集成** - 与ColorVision设备管理系统的深度集成
- **MQTT通信** - 支持MQTT消息通信协议

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 核心引擎功能（设备管理、模板、MQTT、数据库）
- ColorVision.Solution - 解决方案框架和工作区管理
- ColorVision.ImageEditor - 图像编辑和显示功能
- CVCommCore.dll - 通信核心组件

**被引用方式**:
- 作为插件通过manifest.json注册到主程序
- 编译后自动复制到主程序Plugins目录（PostBuild步骤）
- 可独立启动ConoscopeWindow窗口

## 使用方式

### 引用方式

```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.ImageEditor\ColorVision.ImageEditor.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.Solution\ColorVision.Solution.csproj" />
```

### 启动和运行
- 直接运行编译后的可执行文件，启动时显示锥光镜观察窗口（ConoscopeWindow）
- 作为插件加载到ColorVision主程序工作区
- 可通过菜单"视图"打开MVS观察相机窗口
- 支持通过ConoscopeModuleService以工作区标签页形式打开

### 主要功能模块
- **锥光镜测试** - 通过ConoscopeWindow进行光学测试
- **MVS相机控制** - 通过MVSViewWindow进行相机操作
- **语言切换** - 通过主程序语言菜单切换系统语言
- **设备管理** - 相机设备的连接和参数配置

## 开发调试

```bash
# 从解决方案根目录构建
dotnet build build.sln

# 构建特定项目
dotnet build Projects/ProjectStarkSemi/ProjectStarkSemi.csproj

# 运行项目（Windows only）
dotnet run --project Projects/ProjectStarkSemi/ProjectStarkSemi.csproj
```

## 目录说明

- `App.xaml/.cs` - 应用程序入口和初始化
- `ConoscopeWindow.xaml/.cs` - 锥光镜主测试窗口（AvalonDock布局）
- `ConoscopeView.xaml/.cs` - 锥光镜图像/分析视图
- `MVSViewWindow.xaml/.cs` - MVS观察相机视频显示窗口
- `AdvancedExportDialog.xaml/.cs` - 高级数据导出弹窗
- `MVCamera.cs` - MVS相机SDK封装（海康威视）
- `manifest.json` - 插件清单文件
- `Conoscope/` - 锥光镜核心逻辑
  - `ConoscopeManager.cs` - 锥光镜管理器（单例）
  - `ConoscopeConfig.cs` - 配置类（型号选择、通道、滤波）
  - `ConoscopeModelType.cs` - 硬件型号枚举（VA60/VA80）
  - `ConoscopeModelProfile.cs` - 型号配置档案
  - `ConoscopeModuleService.cs` - 模块服务（工作区集成、视图管理）
  - `ConoscopeExportService.cs` - 数据导出服务
  - `ConoscopeColorimetry.cs` - 色度学计算（RGB→XYZ转换）
  - `ConoscopeCoordinateAxis.cs` - 坐标轴管理（极角/方位角转换）
  - `ConoscopeConfigWindow.xaml/.cs` - 配置编辑窗口
  - `ConoscopeImageViewContextMenu.cs` - 图像视图右键菜单
  - `ExportMode.cs` - 导出模式和通道枚举
  - `ImageFilterType.cs` - 图像滤波类型枚举
  - `RgbSample.cs` - RGB采样数据模型
  - `PolarAngleLine.cs` - 极角参考线模型
  - `ConcentricCircleLine.cs` - 同心圆参考线模型
  - `MenuConoscopeWindow.cs` - 锥光镜窗口菜单项
- `Layout/` - 布局管理
  - `DockLayoutManager.cs` - AvalonDock布局持久化管理
- `Menus/` - 菜单定义
  - `ConoscopeMenuIBase.cs` - 锥光镜菜单基类
  - `LayoutMenuItems.cs` - 布局切换菜单项
- `Properties/` - 项目属性和多语言资源文件
- `Assets/Image/` - 图像资源文件

## 配置说明

### 硬件型号配置
- **VA60模式**: 双相机配置（观察相机 + 测量相机），最大角度60°
- **VA80模式**: 单测量相机配置，最大角度80°
- 型号切换触发角度范围联动更新
- 配置文件通过ConoscopeConfig持久化

### 语言配置
- 支持中/英/法/日/韩/俄/繁体中文7种语言
- 语言配置通过LanguageConfig管理
- 资源文件位于Properties/目录（.resx）

### 插件配置
- 插件ID: ProjectStarkSemi
- 插件版本: 1.0
- 入口DLL: ProjectStarkSemi.dll
- 编译后自动复制到主程序Plugins目录

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
- 支持多种图像滤波算法（低通、移动平均、高斯、中值、双边）
- 实时图像显示和伪彩色渲染
- 参考线/同心圆辅助分析叠加

### 界面布局
- 基于AvalonDock的可拖拽面板系统
- 布局持久化（保存/加载/重置）
- 面板显隐切换（控制面板、通道面板、参考曲线、设置面板）
- 主题系统支持（亮色/暗色等）

### 系统集成
- 完整的设备服务架构
- 模板和流程引擎支持
- 授权和日志系统
- 工作区标签页集成

## 相关文档链接

- [插件开发指南](../../docs/02-developer-guide/plugin-development/overview.md)
- [项目架构文档](../../docs/03-architecture/README.md)
- [引擎开发文档](../../docs/02-developer-guide/engine-development/README.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)

## 维护者

ColorVision 项目团队

## 版本历史

参见 [CHANGELOG.md](CHANGELOG.md)
