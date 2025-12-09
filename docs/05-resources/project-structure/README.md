# ColorVision 项目结构

本文档详细介绍 ColorVision 项目的目录结构和各模块组成，帮助开发者快速了解项目组织方式。

## 📁 目录结构总览

```
ColorVision/
├── ColorVision/              # 主程序入口
│   ├── Assets/               # 资源文件
│   ├── Properties/           # 程序集属性
│   ├── Update/               # 更新相关
│   └── Wizards/              # 向导界面
├── Engine/                   # 核心引擎层
│   ├── ColorVision.Engine/   # 主引擎模块
│   ├── cvColorVision/        # 视觉处理核心
│   ├── FlowEngineLib/        # 流程引擎库
│   ├── ColorVision.FileIO/   # 文件IO处理
│   └── ST.Library.UI/        # UI库组件
├── UI/                       # 用户界面层
│   ├── ColorVision.UI/       # 主UI框架
│   ├── ColorVision.Common/   # 通用UI组件
│   ├── ColorVision.Core/     # 核心UI组件
│   ├── ColorVision.Themes/   # 主题管理
│   ├── ColorVision.ImageEditor/  # 图像编辑器
│   ├── ColorVision.Solution/ # 解决方案管理
│   ├── ColorVision.Scheduler/    # 任务调度器
│   ├── ColorVision.Database/ # 数据库UI
│   └── ColorVision.SocketProtocol/ # Socket协议
├── Plugins/                  # 扩展插件层
│   ├── EventVWR/            # 事件查看器插件
│   ├── Pattern/             # 图案检测插件
│   ├── SystemMonitor/       # 系统监控插件
│   ├── ScreenRecorder/      # 屏幕录制插件
│   └── WindowsServicePlugin/ # Windows服务插件
├── Projects/                 # 客户定制项目
│   ├── ProjectARVR/         # ARVR项目
│   ├── ProjectARVRLite/     # ARVR Lite版本
│   ├── ProjectARVRPro/      # ARVR Pro版本
│   ├── ProjectKB/           # KB项目
│   ├── ProjectLUX/          # LUX项目
│   ├── ProjectBlackMura/    # BlackMura项目
│   ├── ProjectHeyuan/       # 河源项目
│   └── ProjectShiyuan/      # 识远项目
├── Core/                     # 核心底层库
│   ├── ColorVisionIcons64/  # 图标资源
│   ├── opencv_cuda/         # OpenCV CUDA支持
│   ├── opencv_helper/       # OpenCV辅助工具
│   └── opencv_opengl/       # OpenCV OpenGL集成
├── DLL/                      # 外部DLL依赖
│   └── scgd_internal_dll/   # 内部DLL
├── Test/                     # 测试项目
│   ├── ColorVision.UI.Tests/ # UI单元测试
│   └── opencv_helper_test/  # OpenCV测试
├── Tools/                    # 工具集
│   └── LicenseGenerator/    # 许可证生成器
├── ColorVisionSetup/         # 安装程序
├── docs/                     # 文档（VitePress站点）
├── Scripts/                  # 构建和自动化脚本
├── scripts/                  # 额外脚本
├── Advanced/                 # 高级功能
├── include/                  # C++头文件
└── packages/                 # NuGet包（第三方库）
```

## 🏗️ 主要模块说明

### ColorVision/ - 主程序

**作用**：应用程序入口，主窗口和应用程序级功能

**关键文件**：
- `App.xaml` / `App.xaml.cs` - 应用程序定义和启动逻辑
- `MainWindow.xaml` / `MainWindow.xaml.cs` - 主窗口
- `EntryClass.cs` - 程序入口类
- `ColorVision.csproj` - 项目文件

**技术栈**：
- .NET 8.0 (net8.0-windows)
- WPF (Windows Presentation Foundation)
- 平台：x64

**详细文档**：
- [入门指南](../getting-started/入门指南.md)
- [主窗口导览](../user-interface-guide/main-window/主窗口导览.md)

---

### Engine/ - 核心引擎层

**作用**：系统核心业务逻辑，包含设备服务、算法模板、流程引擎等

#### Engine/ColorVision.Engine/

**功能**：
- 设备服务管理（相机、光谱仪、电机等）
- 算法模板系统
- 流程引擎集成
- MQTT通信
- 数据库访问

**主要子目录**：
- `Services/` - 设备服务实现
- `Templates/` - 算法模板
- `MQTT/` - MQTT通信协议
- `Media/` - 媒体处理

**详细文档**：
- [Engine组件概览](../engine-components/Engine组件概览.md)
- [ColorVision.Engine](../engine-components/ColorVision.Engine.md)
- [设备服务概览](../device-management/device-services-overview/设备服务概览.md)
- [算法引擎与模板](../algorithm-engine-templates/算法引擎与模板.md)

#### Engine/cvColorVision/

**功能**：视觉处理核心算法库（C++）

**详细文档**：
- [cvColorVision](../engine-components/cvColorVision.md)

#### Engine/FlowEngineLib/

**功能**：流程引擎库，提供可视化流程编辑和执行

**详细文档**：
- [FlowEngineLib](../engine-components/FlowEngineLib.md)
- [流程引擎架构](../architecture/FlowEngineLib-Architecture.md)
- [流程引擎](../algorithm-engine-templates/flow-engine/流程引擎.md)

#### Engine/ColorVision.FileIO/

**功能**：文件IO处理，图像文件读写

**详细文档**：
- [ColorVision.FileIO](../engine-components/ColorVision.FileIO.md)

---

### UI/ - 用户界面层

**作用**：用户界面组件、主题管理、通用控件

#### UI/ColorVision.UI/

**功能**：
- UI框架和基础设施
- 属性编辑器系统
- 插件管理界面
- 菜单和热键系统

**详细文档**：
- [UI组件概览](../ui-components/UI组件概览.md)
- [ColorVision.UI](../ui-components/ColorVision.UI.md)
- [属性编辑器](../user-interface-guide/property-editor/属性编辑器.md)
- [热键系统设计](../ui-components/HotKey系统设计文档.md)

#### UI/ColorVision.Themes/

**功能**：主题管理和样式资源

**详细文档**：
- [ColorVision.Themes](../ui-components/ColorVision.Themes.md)

#### UI/ColorVision.ImageEditor/

**功能**：专业图像编辑工具，ROI绘制，图形标注

**详细文档**：
- [ColorVision.ImageEditor](../ui-components/ColorVision.ImageEditor.md)
- [图像编辑器](../user-interface-guide/image-editor/图像编辑器.md)

#### UI/ColorVision.Scheduler/

**功能**：任务调度器UI，基于Quartz.NET

**详细文档**：
- [ColorVision.Scheduler](../ui-components/ColorVision.Scheduler.md)

#### UI/ColorVision.Solution/

**功能**：解决方案和工程文件管理

**详细文档**：
- [ColorVision.Solution](../ui-components/ColorVision.Solution.md)

#### 其他UI组件

- **ColorVision.Common** - 通用UI组件 ([文档](../ui-components/ColorVision.Common.md))
- **ColorVision.Core** - 核心UI组件 ([文档](../ui-components/ColorVision.Core.md))
- **ColorVision.Database** - 数据库UI ([文档](../ui-components/ColorVision.Database.md))
- **ColorVision.SocketProtocol** - Socket协议 ([文档](../ui-components/ColorVision.SocketProtocol.md))

---

### Plugins/ - 扩展插件层

**作用**：可插拔的扩展功能模块

**插件列表**：

| 插件名称 | 功能说明 | 文档 |
|---------|---------|------|
| EventVWR | Windows事件查看器集成 | - |
| Pattern | 图案检测和分析工具 | [Pattern插件](../plugins/using-standard-plugins/pattern.md) |
| SystemMonitor | 系统性能监控面板 | [系统监控插件](../plugins/system-monitor.md) |
| ScreenRecorder | 屏幕录制功能 | - |
| WindowsServicePlugin | Windows服务集成 | - |

**插件开发**：
- [插件管理](../plugins/plugin-management/插件管理.md)
- [开发插件指南](../plugins/developing-a-plugin.md)
- [使用标准插件](../plugins/using-standard-plugins/使用标准插件.md)

---

### Projects/ - 客户定制项目

**作用**：针对特定客户需求的完整解决方案

**项目列表**：
- **ProjectARVR** - AR/VR 检测项目
- **ProjectARVRLite** - AR/VR Lite 版本
- **ProjectARVRPro** - AR/VR Pro 版本（[README](../../../Projects/ProjectARVRPro/README.md)，[优化计划](../../../Projects/ProjectARVRPro/OPTIMIZATION.md)）
- **ProjectKB** - KB 项目
- **ProjectLUX** - LUX 亮度测量项目
- **ProjectBlackMura** - 黑斑检测项目
- **ProjectHeyuan** - 河源定制项目
- **ProjectShiyuan** - 识远定制项目

每个项目都有自己的 README.md 说明文档。

---

### Core/ - 核心底层库

**作用**：底层C++库和OpenCV集成

**子模块**：
- **opencv_cuda/** - OpenCV CUDA加速
- **opencv_helper/** - OpenCV辅助工具
- **opencv_opengl/** - OpenCV OpenGL集成
- **ColorVisionIcons64/** - 64位图标资源

---

### Test/ - 测试项目

**作用**：单元测试和集成测试

**测试项目**：
- **ColorVision.UI.Tests** - UI组件单元测试（xUnit）
- **opencv_helper_test** - OpenCV辅助工具测试

---

### docs/ - 文档

**作用**：VitePress文档站点源文件

**在线访问**：https://xincheng213618.github.io/scgd_general_wpf/

**文档结构**：
- `getting-started/` - 入门指南
- `architecture/` - 系统架构
- `ui-components/` - UI组件文档
- `engine-components/` - Engine组件文档
- `plugins/` - 插件开发
- `device-management/` - 设备管理
- `user-interface-guide/` - 用户界面指南
- 更多分类...

---

### 其他目录

- **ColorVisionSetup/** - 安装程序（.NET Framework 4.8）
- **Scripts/** - 构建脚本
- **Tools/** - 工具集（如许可证生成器）
- **DLL/** - 外部DLL依赖
- **packages/** - NuGet第三方库包
- **include/** - C++头文件

## 🔗 模块与文档对照

详细的模块与文档映射关系，请参考：[模块文档对照表](module-documentation-map.md)

## 📦 技术栈总览

- **主框架**：.NET 8.0, WPF
- **平台**：Windows x64/ARM64
- **UI库**：HandyControl, WPF Extended Toolkit
- **数据库**：MySQL, SQLite (MySqlConnector)
- **通信**：MQTT (MQTTnet), Socket
- **图像处理**：OpenCvSharp4, OpenCV (C++)
- **任务调度**：Quartz.NET
- **日志**：log4net
- **序列化**：Newtonsoft.Json
- **测试**：xUnit

## 📚 延伸阅读

- [系统架构概览](../introduction/system-architecture/系统架构概览.md)
- [架构运行时](../architecture/architecture-runtime.md)
- [组件交互矩阵](../architecture/component-interactions.md)
- [入门指南](../getting-started/入门指南.md)
- [开发指南](../developer-guide/api-reference/API_参考.md)
