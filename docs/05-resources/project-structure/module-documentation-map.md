# 模块与文档对照表

本文档用于快速定位“代码在哪，先看什么文档”。如果只记一个原则：客户业务先看项目说明，用户操作去使用手册，源码交接去模块参考。

## 顶层目录

| 代码区域 | 关注点 | 首选文档入口 | 补充入口 |
| --- | --- | --- | --- |
| `ColorVision/` | 主程序入口、主窗口、启动参数 | [安装与首次使用](../../00-getting-started/README.md) | [使用手册操作工作流矩阵](../../01-user-guide/operation-workflow-matrix.md)、[主窗口导览](../../01-user-guide/interface/main-window.md)、[UI 组件使用手册](../../01-user-guide/interface/ui-component-handbook.md) |
| `UI/` | WPF 类库、DLL/NuGet 发布、主题、编辑器 | [UI 组件与 DLL 发布](../../04-api-reference/ui-components/README.md) | [UI 组件使用手册](../../01-user-guide/interface/ui-component-handbook.md)、[UI 运行时组件交接手册](../../04-api-reference/ui-components/ui-runtime-handoff.md)、[UI 组件目录](../../04-api-reference/ui-components/control-catalog.md)、[UI DLL 发布场景手册](../../04-api-reference/ui-components/publishing.md)、[UI DLL 发布矩阵](../../04-api-reference/ui-components/publishing.md)、[UI DLL 发布手册](../../04-api-reference/ui-components/publishing.md) |
| `Engine/` | 设备、模板、流程、MQTT、结果 | [Engine 组件与业务交接](../../04-api-reference/engine-components/README.md) | [业务链路矩阵](../../04-api-reference/engine-components/business-flow-matrix.md)、[业务场景交接手册](../../04-api-reference/engine-components/README.md)、[运行时对象目录](../../04-api-reference/engine-components/runtime-object-map.md)、[业务交接](../../04-api-reference/engine-components/README.md)、[设备链路](../../04-api-reference/engine-components/device-service-chain.md)、[模板与 Flow](../../04-api-reference/engine-components/template-flow-chain.md)、[当前算法模板覆盖清单](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)、[结果交接](../../04-api-reference/engine-components/result-handoff-chain.md) |
| `Plugins/` | 通用插件 | [现有插件能力说明](../../04-api-reference/plugins/README.md) | [当前插件文档覆盖清单](../../04-api-reference/plugins/README.md)、[插件运行与交接场景手册](../../04-api-reference/plugins/README.md)、[现有插件现场验收与交接清单](../../04-api-reference/plugins/README.md)、[插件能力与交接矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md)、[插件开发手册](../../02-developer-guide/plugin-development/README.md) |
| `Projects/` | 客户项目包和对接示例 | [项目说明](../../00-projects/README.md) | [项目包能力与交接矩阵](../../04-api-reference/projects/project-capability-matrix.md)、[项目包运行与交接场景手册](../../04-api-reference/projects/README.md)、[项目包总览](../../04-api-reference/projects/README.md)、[项目包交接手册](../../04-api-reference/projects/README.md)、[Engine 业务交接手册](../../04-api-reference/engine-components/README.md) |
| `Web/Backend/` | 插件市场后端 | [插件市场后端](../../02-developer-guide/backend/README.md) | [部署概览](../../02-developer-guide/deployment/overview.md) |
| `Scripts/` | 构建、打包、发布 | [构建与发布脚本](../../02-developer-guide/scripts/README.md) | [开发手册](../../02-developer-guide/README.md) |
| `Test/` | xUnit、native helper、后端和脚本验证 | [测试与验证交接手册](../../02-developer-guide/testing.md) | [开发手册](../../02-developer-guide/README.md) |
| `docs/` | VitePress 文档站 | [附录与资源](../README.md) | 当前页 |

## UI 模块

| 模块 | 文档 |
| --- | --- |
| `UI/ColorVision.Common/` | [ColorVision.Common](../../04-api-reference/ui-components/ColorVision.Common.md) |
| `UI/ColorVision.Themes/` | [ColorVision.Themes](../../04-api-reference/ui-components/ColorVision.Themes.md) |
| `UI/ColorVision.UI/` | [ColorVision.UI](../../04-api-reference/ui-components/ColorVision.UI.md) |
| `UI/ColorVision.Core/` | [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) |
| `UI/ColorVision.Database/` | [ColorVision.Database](../../04-api-reference/ui-components/ColorVision.Database.md) |
| `UI/ColorVision.SocketProtocol/` | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) |
| `UI/ColorVision.Scheduler/` | [ColorVision.Scheduler](../../04-api-reference/ui-components/ColorVision.Scheduler.md) |
| `UI/ColorVision.ImageEditor/` | [ColorVision.ImageEditor](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) |
| `UI/ColorVision.UI.Desktop/` | [ColorVision.UI.Desktop](../../04-api-reference/ui-components/ColorVision.UI.Desktop.md) |
| `UI/ColorVision.Solution/` | [ColorVision.Solution](../../04-api-reference/ui-components/ColorVision.Solution.md) |
| UI 控件、窗口、PropertyGrid、菜单、状态栏 | [UI 组件使用手册](../../01-user-guide/interface/ui-component-handbook.md)、[UI 运行时组件交接手册](../../04-api-reference/ui-components/ui-runtime-handoff.md)、[UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) |

## Engine 模块

| 模块 | 文档 |
| --- | --- |
| Engine 横向业务场景、变更归属和验收 | [Engine 业务链路矩阵](../../04-api-reference/engine-components/business-flow-matrix.md) |
| Engine 具体需求处理步骤、验收和交接记录 | [Engine 业务场景交接手册](../../04-api-reference/engine-components/README.md) |
| Engine 运行时对象、关键类和扩展点 | [Engine 运行时对象目录](../../04-api-reference/engine-components/runtime-object-map.md) |
| `Engine/ColorVision.Engine/` | [ColorVision.Engine](../../04-api-reference/engine-components/ColorVision.Engine.md) |
| `Engine/ColorVision.Engine/Services/` | [Engine 设备服务链路](../../04-api-reference/engine-components/device-service-chain.md) |
| `Engine/ColorVision.Engine/Templates/` | [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md)、[当前算法模板覆盖清单](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)、[BuzProduct 产品业务参数模板](../../04-api-reference/algorithms/templates/buz-product-template.md)、[Validate 判定规则模板](../../04-api-reference/algorithms/templates/validate-rules.md)、[Compliance 结果交接](../../04-api-reference/algorithms/templates/compliance-results.md)、[DataLoad 数据加载模板](../../04-api-reference/algorithms/templates/data-load-template.md)、[Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md)、[SysDictionary 系统字典模板](../../04-api-reference/algorithms/templates/sys-dictionary-template.md)、[FocusPoints 关注点模板](../../04-api-reference/algorithms/templates/focus-points-template.md)、[ImageCropping 图像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md)、[模板菜单入口](../../04-api-reference/algorithms/templates/template-menu-entries.md) |
| `Engine/ColorVision.Engine/Abstractions/`、`Templates/**/ViewHandle*.cs` | [Engine 结果展示与项目交接链路](../../04-api-reference/engine-components/result-handoff-chain.md) |
| `Engine/FlowEngineLib/` | [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) |
| `Engine/cvColorVision/` | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Engine/ColorVision.FileIO/` | [ColorVision.FileIO](../../04-api-reference/engine-components/ColorVision.FileIO.md) |
| `Engine/ST.Library.UI/` | [ST.Library.UI](../../04-api-reference/engine-components/ST.Library.UI.md) |
| `Engine/ColorVision.ShellExtension/` | [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) |

## 当前插件

| 插件目录 | 文档 |
| --- | --- |
| `Plugins/` 横向交接 | [插件运行与交接场景手册](../../04-api-reference/plugins/README.md)、[现有插件现场验收与交接清单](../../04-api-reference/plugins/README.md)、[插件能力与交接矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) |
| `Plugins/Conoscope/` | [Conoscope](../../04-api-reference/plugins/standard-plugins/conoscope.md) |
| `Plugins/Spectrum/` | [Spectrum](../../04-api-reference/plugins/standard-plugins/spectrum.md) |
| `Plugins/SystemMonitor/` | [SystemMonitor](../../04-api-reference/plugins/standard-plugins/system-monitor.md) |
| `Plugins/EventVWR/` | [EventVWR](../../04-api-reference/plugins/standard-plugins/eventvwr.md) |
| `Plugins/WindowsServicePlugin/` | [WindowsServicePlugin](../../04-api-reference/plugins/standard-plugins/windows-service.md) |

## 当前项目包

| 项目目录 | 文档 |
| --- | --- |
| `Projects/` 能力、协议、输出横向对照 | [项目包能力与交接矩阵](../../04-api-reference/projects/project-capability-matrix.md) |
| `Projects/` 现场问题、触发、流程、导出和打包场景 | [项目包运行与交接场景手册](../../04-api-reference/projects/README.md) |
| `Projects/` 通用交接 | [项目包交接手册](../../04-api-reference/projects/README.md) |
| `Projects/ProjectARVR/` | [ProjectARVR](../../04-api-reference/projects/project-arvr.md) |
| `Projects/ProjectARVRLite/` | [ProjectARVRLite](../../04-api-reference/projects/project-arvr-lite.md) |
| `Projects/ProjectARVRPro/` | [ProjectARVRPro](../../04-api-reference/projects/project-arvr-pro.md) |
| `Projects/ProjectARVRPro.IntegrationDemo/` | [ProjectARVRPro.IntegrationDemo](../../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| `Projects/ProjectBlackMura/` | [ProjectBlackMura](../../04-api-reference/projects/project-black-mura.md) |
| `Projects/ProjectHeyuan/` | [ProjectHeyuan](../../04-api-reference/projects/project-heyuan.md) |
| `Projects/ProjectKB/` | [ProjectKB](../../04-api-reference/projects/project-kb.md) |
| `Projects/ProjectLUX/` | [ProjectLUX](../../04-api-reference/projects/project-lux.md) |
| `Projects/ProjectShiyuan/` | [ProjectShiyuan](../../04-api-reference/projects/project-shiyuan.md) |

## 按任务查找

| 任务 | 建议路径 |
| --- | --- |
| 新增设备服务 | [Engine 设备服务链路](../../04-api-reference/engine-components/device-service-chain.md) -> `Services/Devices/` |
| 按业务能力定位 Engine 入口 | [Engine 业务链路矩阵](../../04-api-reference/engine-components/business-flow-matrix.md) |
| 按具体需求处理 Engine 变更 | [Engine 业务场景交接手册](../../04-api-reference/engine-components/README.md) |
| 发布 UI DLL | [UI DLL 发布场景手册](../../04-api-reference/ui-components/publishing.md)、[UI DLL 发布手册](../../04-api-reference/ui-components/publishing.md) |
| 按 UI 窗口或组件查使用和排查路径 | [UI 组件使用手册](../../01-user-guide/interface/ui-component-handbook.md)、[UI 运行时组件交接手册](../../04-api-reference/ui-components/ui-runtime-handoff.md) |
| 按操作场景查使用路径 | [使用手册操作工作流矩阵](../../01-user-guide/operation-workflow-matrix.md) |
| 选择测试与验收命令 | [测试与验证交接手册](../../02-developer-guide/testing.md) |
| 开发插件 | [插件运行与交接场景手册](../../04-api-reference/plugins/README.md) -> [插件开发手册](../../02-developer-guide/plugin-development/README.md) -> [现有插件能力说明](../../04-api-reference/plugins/README.md) -> [现有插件现场验收与交接清单](../../04-api-reference/plugins/README.md) |
| 维护客户项目 | [项目说明](../../00-projects/README.md) -> [项目包能力与交接矩阵](../../04-api-reference/projects/project-capability-matrix.md) -> [项目包运行与交接场景手册](../../04-api-reference/projects/README.md) -> [项目包交接手册](../../04-api-reference/projects/README.md) -> 对应项目页 |
| 修改流程节点 | [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) -> [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md) |
| 修改算法模板 | [当前算法模板覆盖清单](../../04-api-reference/algorithms/current-algorithm-template-coverage.md) -> [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) -> [算法与模板](../../04-api-reference/algorithms/README.md) |
| 维护发光区定位 | [FindLightArea 发光区定位模板](../../04-api-reference/algorithms/templates/find-light-area.md) -> [ROI 原语](../../04-api-reference/algorithms/primitives/roi.md) |
| 维护 JND 结果 | [JND 模板](../../04-api-reference/algorithms/templates/jnd-template.md) -> [POI 模板](../../04-api-reference/algorithms/templates/poi-template.md) -> [ProjectShiyuan](../../04-api-reference/projects/project-shiyuan.md) |
| 维护 LED 灯条或灯珠检测 | [LED 检测模板](../../04-api-reference/algorithms/templates/led-detection.md) -> [JSON 模板](../../04-api-reference/algorithms/templates/json-templates.md) |
| 维护产品业务参数 | [BuzProduct 产品业务参数模板](../../04-api-reference/algorithms/templates/buz-product-template.md) -> [Validate 判定规则模板](../../04-api-reference/algorithms/templates/validate-rules.md) -> [项目包能力与交接矩阵](../../04-api-reference/projects/project-capability-matrix.md) |
| 维护判定规则模板 | [Validate 判定规则模板](../../04-api-reference/algorithms/templates/validate-rules.md) -> [BuzProduct 产品业务参数模板](../../04-api-reference/algorithms/templates/buz-product-template.md) -> [Compliance 结果交接](../../04-api-reference/algorithms/templates/compliance-results.md) |
| 排查 Compliance 判定结果 | [Compliance 结果交接](../../04-api-reference/algorithms/templates/compliance-results.md) -> [Validate 判定规则模板](../../04-api-reference/algorithms/templates/validate-rules.md) -> [Engine 结果展示与项目交接链路](../../04-api-reference/engine-components/result-handoff-chain.md) |
| 配置 DataLoad 数据加载 | [DataLoad 数据加载模板](../../04-api-reference/algorithms/templates/data-load-template.md) -> [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) -> [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md) |
| 维护 Matching 模板匹配 | [Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md) -> [Engine 结果展示与项目交接链路](../../04-api-reference/engine-components/result-handoff-chain.md) -> [ROI 原语](../../04-api-reference/algorithms/primitives/roi.md) |
| 维护系统字典默认参数 | [SysDictionary 系统字典模板](../../04-api-reference/algorithms/templates/sys-dictionary-template.md) -> [模板管理](../../04-api-reference/algorithms/templates/template-management.md) -> [Templates API 参考](../../04-api-reference/algorithms/templates/api-reference.md) |
| 维护 FocusPoints 发光区参数 | [FocusPoints 关注点模板](../../04-api-reference/algorithms/templates/focus-points-template.md) -> [FindLightArea 发光区定位模板](../../04-api-reference/algorithms/templates/find-light-area.md) -> [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) |
| 维护 ImageCropping 图像裁剪 | [ImageCropping 图像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md) -> [Engine 结果展示与项目交接链路](../../04-api-reference/engine-components/result-handoff-chain.md) -> [ROI 原语](../../04-api-reference/algorithms/primitives/roi.md) |
| 新增或排查模板菜单入口 | [模板菜单入口](../../04-api-reference/algorithms/templates/template-menu-entries.md) -> [模板管理](../../04-api-reference/algorithms/templates/template-management.md) -> [插件开发手册](../../02-developer-guide/plugin-development/README.md) |
| 修改算法结果展示 | [Engine 结果展示与项目交接链路](../../04-api-reference/engine-components/result-handoff-chain.md) |
| 从类名定位 Engine 业务链路 | [Engine 运行时对象目录](../../04-api-reference/engine-components/runtime-object-map.md) |
| 打包插件或项目 | [构建与发布脚本](../../02-developer-guide/scripts/README.md) |

## 使用原则

- 先看章节首页，再进入具体模块页。
- 历史页面只作为说明，不作为当前功能承诺。
- 如果文档与源码不一致，优先更新对应的项目说明、插件能力页或模块参考页。
