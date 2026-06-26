# 参考资料

本章是源码和模块参考入口。它不是第一阅读路径；新用户先看 [快速上手](../00-getting-started/quick-start.md)，开发者先看 [开发手册](../02-developer-guide/README.md)。

## 模块入口

| 模块 | 源码范围 | 入口 |
| --- | --- | --- |
| UI 组件 | `UI/` | [UI 组件](./ui-components/README.md) |
| Engine 组件 | `Engine/` | [Engine 组件](./engine-components/README.md) |
| 算法与模板 | `Engine/ColorVision.Engine/Templates/` | [算法与模板](./algorithms/README.md) |
| 插件 | `Plugins/` | [现有插件能力](./plugins/README.md) |
| 项目包 | `Projects/` | [项目包总览](./projects/README.md) |
| 扩展点 | `Engine/FlowEngineLib/`、`UI/ColorVision.UI/` | [扩展点](./extensions/README.md) |
| Flow 节点 | FlowEngine 节点清单 | [Flow 节点摘要](./flow_nodes_summary.md) |

## 使用原则

- 参考页必须能回到当前源码目录、项目文件、manifest、脚本或关键类。
- 不再长期维护一次性记录、完整生成参考和临时计划。
- 如果参考页和源码不一致，以源码和实际构建结果为准，并优先更新对应 README。
