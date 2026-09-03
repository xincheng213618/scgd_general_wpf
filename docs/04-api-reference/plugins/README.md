---
knowledge_id: "plugins.index"
knowledge_type: "index"
status: "current"
summary: "按程序集装载、产物交付、插件能力比较和模块操作定位权威主题与源码。"
aliases: ["当前有哪些通用插件", "插件功能在哪里", "新建插件", "插件不加载从哪里查"]
code_paths: ["Plugins", "UI/ColorVision.UI/Plugins/PluginLoader.cs", "Plugins/Directory.Build.props", "PluginProject.HostCopy.targets"]
test_paths: []
related: ["plugins.model", "plugins.getting-started", "plugins.capabilities", "plugins.conoscope", "plugins.spectrum", "plugins.system-monitor", "plugins.windows-service", "plugins.pattern", "platform.extensibility"]
---

# 插件装配与模块知识入口

`Plugins/` 保存通用插件源码，`UI/ColorVision.UI/Plugins/` 实现宿主的装载与产物管理，`Scripts/` 提供交付入口。插件扩展宿主能力，但不会因有 DLL 或 manifest 就自动具备菜单、隔离运行或热卸载能力。按当前问题进入一份规范主题，再核对其源码和验证边界。

## 按代码责任定位

| 问题 | 规范主题 | 所属实现 |
| --- | --- | --- |
| 应放在插件、Engine 服务、模板还是项目包 | [扩展责任边界](../../02-developer-guide/core-concepts/extensibility.md) | 已有抽象与各模块边界 |
| manifest 已识别但加载失败；DLL 加载了却没菜单 | [插件装载与扩展发现](../../02-developer-guide/plugin-development/overview.md) | `PluginLoader`、`AssemblyHandler`、各 provider 消费者 |
| 新项目产物、HostCopy、cvxp 安装替换或导出 | [插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md) | `PluginProject.HostCopy.targets`、宿主产物管理与 `package_cvxp.py` |
| 当前有哪些插件；某个插件的设备、设置、窗口或数据库行为 | [当前插件与单模块主题](./plugin-capability-matrix.md#当前源码插件总表) | `Plugins/<Name>/` |
| 横向检查插件扩展点与外部依赖 | [插件依赖与接入矩阵](./plugin-capability-matrix.md) | 各插件菜单、状态、配置和 native 接入 |
| 客户判定、MES 字段或项目专用流程 | [项目知识入口](../../04-api-reference/projects/README.md) | `Projects/`，不是通用插件宿主 |

## 修改与验证

修改装载、依赖检查或包结构时，更新相应共用契约；修改某个插件的业务行为时，更新该模块主题。新增或移除模块后更新横向矩阵及相关主题元数据，再生成知识地图和网站导航；只有问题路由变化时才修改本入口。

本地构建、运行插件和发布是不同动作。创建文档或诊断加载失败不授权启动设备、修改系统服务或上传包；普通插件、Spectrum 双通道、客户项目包和主程序的发布对象与入口见[产物契约](../../02-developer-guide/plugin-development/getting-started.md)及对应模块页。本入口不声明覆盖所有插件的统一运行测试。
