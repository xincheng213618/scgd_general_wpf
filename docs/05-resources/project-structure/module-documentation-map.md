# 模块与文档对照表

本文档提供项目目录与相关文档的快速映射，帮助开发者快速定位所需文档。

## 📋 目录结构到文档的映射

| 项目目录 | 组件类型 | 主要文档 | 说明 |
|---------|---------|---------|------|
| **ColorVision/** | 主程序 | [入门指南](../getting-started/入门指南.md) \<br> [主窗口导览](../user-interface-guide/main-window/主窗口导览.md) | WPF应用程序入口 |
| **Engine/** | 核心引擎 | [Engine组件概览](../engine-components/Engine组件概览.md) | 引擎层总入口 |
| └─ ColorVision.Engine/ | 主引擎 | [ColorVision.Engine](../engine-components/ColorVision.Engine.md) \<br> [设备服务概览](../device-management/device-services-overview/设备服务概览.md) \<br> [算法引擎与模板](../algorithm-engine-templates/算法引擎与模板.md) | 核心业务逻辑 |
| └─ cvColorVision/ | 视觉处理 | [cvColorVision](../engine-components/cvColorVision.md) | C++视觉算法库 |
| └─ FlowEngineLib/ | 流程引擎 | [FlowEngineLib](../engine-components/FlowEngineLib.md) \<br> [流程引擎架构](../architecture/FlowEngineLib-Architecture.md) \<br> [流程引擎](../algorithm-engine-templates/flow-engine/流程引擎.md) | 可视化流程编辑器 |
| └─ ColorVision.FileIO/ | 文件IO | [ColorVision.FileIO](../engine-components/ColorVision.FileIO.md) | 文件读写处理 |
| └─ ST.Library.UI/ | UI库 | [ST.Library.UI](../engine-components/ST.Library.UI.md) | UI辅助库 |
| **UI/** | UI层 | [UI组件概览](../ui-components/UI组件概览.md) | UI层总入口 |
| └─ ColorVision.UI/ | UI框架 | [ColorVision.UI](../ui-components/ColorVision.UI.md) \<br> [属性编辑器](../user-interface-guide/property-editor/属性编辑器.md) \<br> [热键系统设计](../ui-components/HotKey系统设计文档.md) | 主UI框架 |
| └─ ColorVision.Common/ | 通用组件 | [ColorVision.Common](../ui-components/ColorVision.Common.md) | 通用UI控件 |
| └─ ColorVision.Core/ | 核心组件 | [ColorVision.Core](../ui-components/ColorVision.Core.md) | 核心UI组件 |
| └─ ColorVision.Themes/ | 主题 | [ColorVision.Themes](../ui-components/ColorVision.Themes.md) | 主题和样式 |
| └─ ColorVision.ImageEditor/ | 图像编辑器 | [ColorVision.ImageEditor](../ui-components/ColorVision.ImageEditor.md) \<br> [图像编辑器](../user-interface-guide/image-editor/图像编辑器.md) | 专业图像编辑 |
| └─ ColorVision.Solution/ | 解决方案 | [ColorVision.Solution](../ui-components/ColorVision.Solution.md) | 工程文件管理 |
| └─ ColorVision.Scheduler/ | 调度器 | [ColorVision.Scheduler](../ui-components/ColorVision.Scheduler.md) | 任务调度UI |
| └─ ColorVision.Database/ | 数据库UI | [ColorVision.Database](../ui-components/ColorVision.Database.md) | 数据库界面 |
| └─ ColorVision.SocketProtocol/ | Socket | [ColorVision.SocketProtocol](../ui-components/ColorVision.SocketProtocol.md) | Socket协议 |
| **Plugins/** | 插件 | [插件管理](../plugins/plugin-management/插件管理.md) \<br> [开发插件指南](../plugins/developing-a-plugin.md) | 插件系统 |
| └─ Pattern/ | 图案检测 | [Pattern插件](../plugins/using-standard-plugins/pattern.md) | 图案检测插件 |
| └─ Spectrum/ | 光谱仪 | [Spectrum插件](/04-api-reference/plugins/standard-plugins/spectrum.md) | 光谱仪测试插件 |
| └─ SystemMonitor/ | 系统监控 | [系统监控插件](../plugins/system-monitor.md) | 性能监控 |
| └─ EventVWR/ | 事件查看 | [使用标准插件](../plugins/using-standard-plugins/使用标准插件.md) | 事件查看器 |
| └─ ScreenRecorder/ | 屏幕录制 | - | 录屏功能 |
| └─ WindowsServicePlugin/ | 服务插件 | - | Windows服务 |
| **Projects/** | 客户项目 | 各项目README.md | 定制化项目 |
| **Core/** | 底层库 | - | C++底层库 |
| **Test/** | 测试 | - | 单元测试 |
| **Tools/** | 工具 | - | 辅助工具 |
| **ColorVisionSetup/** | 安装程序 | [自动更新](../update/README.md) | 安装和更新 |
| **docs/** | 文档 | [在线文档](https://xincheng213618.github.io/scgd_general_wpf/) | VitePress站点 |

## 🎯 按功能域查找文档

### 设备管理相关
- **目录**：`Engine/ColorVision.Engine/Services/`
- **文档**：
  - [设备服务概览](../device-management/device-services-overview/设备服务概览.md)
  - [添加与配置设备](../device-management/adding-configuring-devices/添加与配置设备.md)
  - [相机服务](../device-management/camera-service/相机服务.md)
  - [相机参数配置](../camera-service/camera-configuration/相机参数配置.md)
  - [物理相机管理](../camera-service/physical-camera-management/物理相机管理.md)
  - [校准服务](../device-management/calibration-service/校准服务.md)
  - [电机服务](../device-management/motor-service/电机服务.md)
  - [文件服务](../device-management/file-server-service/文件服务.md)
  - [流程设备服务](../device-management/flow-device-service/流程设备服务.md)
  - [源测量单元(SMU)服务](../device-management/source-measure-unit-smu-service/源测量单元_(SMU)_服务.md)

### 算法与模板相关
- **目录**：`Engine/ColorVision.Engine/Templates/`
- **文档**：
  - [算法引擎与模板](../algorithm-engine-templates/算法引擎与模板.md)
  - [模板管理](../algorithm-engine-templates/template-management/模板管理.md)
  - [基于JSON的通用模板](../algorithm-engine-templates/json-based-templates/基于JSON的通用模板.md)
  - [通用算法模块](../algorithm-engine-templates/common-algorithm-primitives/通用算法模块.md)
  - [Templates分析总结](../algorithm-engine-templates/templates-architecture/Templates分析总结.md)
  - [Templates架构设计](../algorithm-engine-templates/templates-architecture/Templates架构设计.md)
  - [ARVR模板详解](../algorithm-engine-templates/templates-architecture/ARVR模板详解.md)
  - [POI模板详解](../algorithm-engine-templates/templates-architecture/POI模板详解.md)

### 流程引擎相关
- **目录**：`Engine/FlowEngineLib/`
- **文档**：
  - [流程引擎](../algorithm-engine-templates/flow-engine/流程引擎.md)
  - [FlowEngineLib架构](../architecture/FlowEngineLib-Architecture.md)
  - [流程引擎概览](../flow-engine/flow-engine-overview.md)
  - [状态模型](../flow-engine/state-model.md)
  - [扩展点](../flow-engine/extensibility-points.md)
  - [节点开发指南](../extensibility/FlowEngineLib-NodeDevelopment.md)

### UI组件相关
- **目录**：`UI/`
- **文档**：
  - [UI组件概览](../ui-components/UI组件概览.md)
  - [主窗口导览](../user-interface-guide/main-window/主窗口导览.md)
  - [图像编辑器](../user-interface-guide/image-editor/图像编辑器.md)
  - [属性编辑器](../user-interface-guide/property-editor/属性编辑器.md)
  - [日志查看器](../user-interface-guide/log-viewer/日志查看器.md)

### 架构设计相关
- **文档**：
  - [系统架构概览](../introduction/system-architecture/系统架构概览.md)
  - [架构运行时](../architecture/architecture-runtime.md)
  - [组件交互矩阵](../architecture/component-interactions.md)
  - [ColorVision.Engine重构计划](../architecture/ColorVision.Engine-Refactoring-README.md)

### 插件开发相关
- **目录**：`Plugins/`
- **文档**：
  - [插件管理](../plugins/plugin-management/插件管理.md)
  - [开发插件指南](../plugins/developing-a-plugin.md)
  - [插件生命周期](../plugins/plugin-lifecycle.md)
  - [使用标准插件](../plugins/using-standard-plugins/使用标准插件.md)

### 数据存储相关
- **文档**：
  - [数据存储概览](../data-storage/README.md)

### 部署和更新相关
- **目录**：`ColorVisionSetup/`
- **文档**：
  - [部署文档](../deployment/README.md)
  - [自动更新](../update/README.md)
  - [更新日志窗口](../update/changelog-window.md)

### 性能优化相关
- **文档**：
  - [性能优化指南](../performance/README.md)

### 安全与权限相关
- **文档**：
  - [安全与权限控制](../security/README.md)
  - [RBAC模型](../rbac/rbac-model.md)

### 扩展性开发相关
- **文档**：
  - [扩展性开发](../extensibility/README.md)

### 故障排查相关
- **文档**：
  - [故障排除](../troubleshooting/故障排除.md)

## 🔍 按开发任务查找

### 我想添加新设备
1. 了解设备服务架构：[设备服务概览](../device-management/device-services-overview/设备服务概览.md)
2. 查看现有设备实现：`Engine/ColorVision.Engine/Services/Devices/`
3. 参考添加设备文档：[添加与配置设备](../device-management/adding-configuring-devices/添加与配置设备.md)

### 我想开发插件
1. 阅读插件开发指南：[开发插件指南](../plugins/developing-a-plugin.md)
2. 了解插件管理机制：[插件管理](../plugins/plugin-management/插件管理.md)
3. 查看标准插件示例：`Plugins/Pattern/`, `Plugins/Spectrum/`, `Plugins/SystemMonitor/`
4. 参考插件生命周期：[插件生命周期](../plugins/plugin-lifecycle.md)

### 我想添加算法模板
1. 了解模板系统：[算法引擎与模板](../algorithm-engine-templates/算法引擎与模板.md)
2. 查看模板架构：[Templates架构设计](../algorithm-engine-templates/templates-architecture/Templates架构设计.md)
3. 参考现有模板：`Engine/ColorVision.Engine/Templates/`
4. 了解JSON模板：[基于JSON的通用模板](../algorithm-engine-templates/json-based-templates/基于JSON的通用模板.md)

### 我想自定义UI组件
1. 了解UI架构：[UI组件概览](../ui-components/UI组件概览.md)
2. 查看ColorVision.UI：[ColorVision.UI](../ui-components/ColorVision.UI.md)
3. 了解主题系统：[ColorVision.Themes](../ui-components/ColorVision.Themes.md)
4. 参考属性编辑器：[属性编辑器](../user-interface-guide/property-editor/属性编辑器.md)

### 我想添加流程节点
1. 了解流程引擎：[流程引擎](../algorithm-engine-templates/flow-engine/流程引擎.md)
2. 查看FlowEngineLib架构：[FlowEngineLib架构](../architecture/FlowEngineLib-Architecture.md)
3. 阅读节点开发指南：[节点开发指南](../extensibility/FlowEngineLib-NodeDevelopment.md)
4. 参考现有节点：`Engine/FlowEngineLib/`

### 我想理解系统架构
1. 从概览开始：[系统架构概览](../introduction/system-architecture/系统架构概览.md)
2. 了解运行时：[架构运行时](../architecture/architecture-runtime.md)
3. 查看组件交互：[组件交互矩阵](../architecture/component-interactions.md)
4. 参考项目结构：[项目结构总览](README.md)

## 📖 新手推荐阅读顺序

1. **第一步**：[什么是ColorVision](../introduction/what-is-colorvision/什么是_ColorVision_.md)
2. **第二步**：[入门指南](../getting-started/入门指南.md)
3. **第三步**：[项目结构总览](README.md) ← 当前文档
4. **第四步**：[系统架构概览](../introduction/system-architecture/系统架构概览.md)
5. **第五步**：根据你的角色选择：
   - 使用者：[主窗口导览](../user-interface-guide/main-window/主窗口导览.md)
   - 开发者：[开发指南](../developer-guide/api-reference/API_参考.md)
   - 插件开发：[开发插件指南](../plugins/developing-a-plugin.md)

## 🚀 快速链接

- 📘 [在线文档首页](https://xincheng213618.github.io/scgd_general_wpf/)
- 🏠 [GitHub仓库](https://github.com/xincheng213618/scgd_general_wpf)
- 📝 [更新日志](https://github.com/xincheng213618/scgd_general_wpf/blob/master/CHANGELOG.md)
- 🤝 [贡献指南](https://github.com/xincheng213618/scgd_general_wpf/blob/master/CONTRIBUTING.md)
