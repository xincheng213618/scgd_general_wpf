---
knowledge_id: "platform.product"
knowledge_type: "reference"
status: "current"
summary: "ColorVision 的设备、流程、图像分析、结果、插件与客户项目能力，以及从任务进入文档的方法。"
aliases: ["ColorVision是什么","视觉检测平台","ColorVision有哪些功能","产品概览"]
code_paths: ["ColorVision/ColorVision.csproj","Engine/ColorVision.Engine/ColorVision.Engine.csproj","UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj","UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj"]
test_paths: []
related: ["platform.system","delivery.start","algorithms.index","engine.results","projects.index","plugins.index","copilot.runtime"]
---

# ColorVision 概览

ColorVision 是用于光电视觉检测的 Windows WPF 桌面平台，将设备接入、可视化流程、图像分析和结果处理组织在同一宿主中。插件提供扩展能力，客户项目提供专用流程、判定、协议与报表；实际可用功能取决于所安装模块、配置和设备环境。

## 按任务查找功能

| 要完成的工作 | 文档入口 |
| --- | --- |
| 安装、准备构建环境或首次打开图像 | [安装、构建与运行](./README.md) |
| 连接相机并配置采集 | [相机服务](../01-user-guide/devices/camera.md)、[物理相机与许可证](../01-user-guide/devices/camera-management.md) |
| 编排流程、选择节点并执行 | [流程设计](../01-user-guide/workflow/design.md)、[执行与结果](../01-user-guide/workflow/execution.md)、[节点入口](../04-api-reference/flow_nodes_summary.md) |
| 查看图像、绘图、分析或导出图像 | [ImageEditor](../04-api-reference/ui-components/ColorVision.ImageEditor.md) |
| 查找算法、参数、ROI、POI 或本地定位 | [算法与模板入口](../04-api-reference/algorithms/README.md) |
| 查询历史结果、追踪图像与算法输出 | [Engine 结果链](../04-api-reference/engine-components/result-handoff-chain.md) |
| 导入导出设置、流程或项目结果 | [导入导出](../01-user-guide/data-management/export-import.md) |
| 安装插件、接入光谱或其他扩展模块 | [插件入口](../04-api-reference/plugins/README.md) |
| 配置 ARVR、LUX、KB 等客户功能 | [客户项目](../04-api-reference/projects/README.md) |
| 使用 Copilot 的模型、会话和工具能力 | [Copilot](../02-developer-guide/core-concepts/copilot-agent-runtime.md) |
| 更新版本、重新安装或管理程序备份 | [检查更新与程序备份](../02-developer-guide/deployment/auto-update.md) |

这些入口指向各能力的当前说明：操作前提、参数、输入输出、故障定位及验证边界集中维护在所属主题，不需要通过版本修改记录拼接使用方法。

## 理解运行范围

图像编辑、本地算法、Engine 服务算法和客户检测流程有各自入口。部分功能在本机计算，部分依赖数据库、MQTT、外部算法服务或设备；本地图像能显示不意味着所有服务已就绪。

设备类型与服务对象的存在也不保证任意型号都可用。型号、驱动、校准、协议、结果判定和报表字段应按对应设备或客户项目核对。平台提供这些连接和扩展机制，检测精度、节拍与生产验收由实际项目和样本验证。

宿主、共享 UI、Engine、插件和客户项目的源码责任见[系统职责与调用边界](../03-architecture/overview/system-overview.md)。新增能力先沿已有扩展点定位；不能仅凭菜单或类名推断执行位置。

## 直接查找问题

网站可按功能、界面名称、配置项、API 或错误信息搜索。本地使用[知识地图](../knowledge/index.md)或 `knowledge.mjs search`，读取命中的主题和源码；查询方法见[知识使用约定](../README.md)。索引是定位工具，尚未落地的方案会标为 `planned`，不会作为已有功能承诺。
