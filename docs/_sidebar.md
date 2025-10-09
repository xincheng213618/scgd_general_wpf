<!-- _sidebar.md -->

- [项目首页](/)

## 🚀 入门

- [项目简介](introduction/简介)
- [什么是 ColorVision](introduction/what-is-colorvision/什么是_ColorVision_)
- [主要特性](introduction/key-features/主要特性)
- [入门指南](getting-started/入门指南)
- [快速上手](getting-started/quick-start/快速上手)
- [系统要求](getting-started/prerequisites/系统要求)
- [安装指南](getting-started/installation/安装_ColorVision)

## 🏗️ 架构与模块

- [系统架构概览](introduction/system-architecture/系统架构概览)
- [架构运行时](architecture/architecture-runtime)
- [组件交互矩阵](architecture/component-interactions)

### UI组件

- [UI组件概览](ui-components/UI组件概览)
- [ColorVision.UI](ui-components/ColorVision.UI)
- [ColorVision.Common](ui-components/ColorVision.Common)
- [ColorVision.Core](ui-components/ColorVision.Core)
- [ColorVision.Themes](ui-components/ColorVision.Themes)
- [ColorVision.ImageEditor](ui-components/ColorVision.ImageEditor)
- [ColorVision.Solution](ui-components/ColorVision.Solution)
- [ColorVision.Scheduler](ui-components/ColorVision.Scheduler)
- [ColorVision.Database](ui-components/ColorVision.Database)
- [ColorVision.SocketProtocol](ui-components/ColorVision.SocketProtocol)

### Engine组件

- [Engine组件概览](engine-components/Engine组件概览)
- [ColorVision.Engine](engine-components/ColorVision.Engine)
- [ColorVision.FileIO](engine-components/ColorVision.FileIO)
- [cvColorVision](engine-components/cvColorVision)
- [FlowEngineLib](../Engine/FlowEngineLib/README)

### 核心模块

- [ColorVision.Core](../UI/ColorVision.Core/README) - C++互操作核心接口
- [ColorVision.Database](../UI/ColorVision.Database/README) - 数据库访问层
- [ColorVision.SocketProtocol](../UI/ColorVision.SocketProtocol/README) - 网络通信协议
- [ColorVision.Scheduler](../UI/ColorVision.Scheduler/README) - 任务调度系统
- [插件系统概述](../Plugins/README) - 插件架构和标准插件
- [ProjectShiyuan](../Projects/ProjectShiyuan/README) - 世源科技定制项目

## 🔌 插件系统

- [插件管理](plugins/plugin-management/插件管理)
- [使用标准插件](plugins/using-standard-plugins/使用标准插件)
  - [Pattern Plugin - 图卡生成工具](plugins/using-standard-plugins/pattern)
- [插件生命周期](plugins/plugin-lifecycle)
- [开发插件指南](plugins/developing-a-plugin)

## ⚙️ 流程引擎与算法

- [流程引擎](algorithm-engine-templates/flow-engine/流程引擎)
- [流程引擎概览](flow-engine/flow-engine-overview)
- [状态模型](flow-engine/state-model)
- [扩展点](flow-engine/extensibility-points)
- [算法引擎与模板](algorithm-engine-templates/算法引擎与模板)
- [模板管理](algorithm-engine-templates/template-management/模板管理)
- [基于JSON的通用模板](algorithm-engine-templates/json-based-templates/基于JSON的通用模板)
- [通用算法模块](algorithm-engine-templates/common-algorithm-primitives/通用算法模块)
- [特定领域算法模板](algorithm-engine-templates/specialized-algorithms/特定领域算法模板)
- [算法文档模板](algorithms/_template)

### 通用算法原语

- [ROI (感兴趣区域)](common-algorithm-primitives/roi-region-of-interest/ROI_\(感兴趣区域\))
- [POI (关注点)](common-algorithm-primitives/poi-point-of-interest/POI_\(关注点\))

## 📱 设备管理

### 设备服务

- [设备服务概览](device-management/device-services-overview/设备服务概览)
- [添加与配置设备](device-management/adding-configuring-devices/添加与配置设备)

### 专用服务

- [相机服务](device-management/camera-service/相机服务)
  - [物理相机管理](camera-service/physical-camera-management/物理相机管理)
  - [相机参数配置](camera-service/camera-configuration/相机参数配置)
- [校准服务](device-management/calibration-service/校准服务)
- [电机服务](device-management/motor-service/电机服务)
- [文件服务](device-management/file-server-service/文件服务)
- [流程设备服务](device-management/flow-device-service/流程设备服务)
- [源测量单元 (SMU) 服务](device-management/source-measure-unit-smu-service/源测量单元_\(SMU\)_服务)

## 🖥️ 用户界面

- [主窗口导览](user-interface-guide/main-window/主窗口导览)
- [图像编辑器](user-interface-guide/image-editor/图像编辑器)
- [属性编辑器](user-interface-guide/property-editor/属性编辑器)
- [日志查看器](user-interface-guide/log-viewer/日志查看器)

## 📚 开发指南

- [故障排除](troubleshooting/故障排除)
- [常见问题与解决方案](troubleshooting/common-issues/常见问题与解决方案)
- [性能优化指南](performance/)
- [扩展性开发](extensibility/)
- [安全与权限控制](security/)
- [RBAC 模型](rbac/rbac-model)
- [API 参考](developer-guide/api-reference/API_参考)
- [ColorVision API V1.1](ColorVision%20API%20V1.1)

## 📦 部署与更新

- [数据存储概览](data-storage/)
- [部署文档](deployment/)
- [更新日志](changelog/)

- [自动更新](update/)
  - [更新日志窗口](update/changelog-window)