# 项目包总览

本页是 `Projects/` 的源码参考入口。业务入口见 [项目说明](../../00-projects/README.md)。

## 当前项目页

如果要横向比较协议、触发方式、结果出口和最小验收，先看 [项目横向速查](./project-capability-matrix.md)。

| 项目 | 源码目录 | 入口 |
| --- | --- | --- |
| ProjectARVRPro | `Projects/ProjectARVRPro/` | [ProjectARVRPro](./project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | `Projects/ProjectARVRPro.IntegrationDemo/` | [Integration Demo](./project-arvr-pro-integration-demo.md) |
| ProjectKB | `Projects/ProjectKB/` | [ProjectKB](./project-kb.md) |
| ProjectLUX | `Projects/ProjectLUX/` | [ProjectLUX](./project-lux.md) |

已停用项目的源码快照和恢复方式记录在仓库的 `Projects/ARCHIVED.md`。

## 项目包和插件的区别

项目包通常也会复制到主程序 `Plugins/<Name>/`，但核心目标是交付客户业务流程，而不是提供通用工具。

| 层次 | 典型内容 |
| --- | --- |
| 插件集成层 | `manifest.json`、菜单入口、窗口单例 |
| 流程组织层 | `ProcessManager`、`ProcessGroup`、`ProcessMeta` |
| Engine 绑定层 | `FlowTemplate`、`TemplateFlow.Params`、FlowEngine |
| 业务判定层 | `IProcess.Execute()`、`Recipe/`、`Fix/` |
| 通信层 | Socket、MES、串口、Modbus |
| 结果层 | `ObjectiveTestResult`、结果窗口、CSV/XLSX |

## 打包

```powershell
Scripts\package_project.bat ProjectLUX
```

## 维护要求

- 每个 `Projects/<Name>/` 保留项目 README、CHANGELOG、manifest 和 docs 项目页。
- 修改协议、流程组织、结果出口或打包行为时，同步更新对应项目页和项目 README。
