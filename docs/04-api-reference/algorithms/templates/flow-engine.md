# 流程引擎

本页描述 `Engine/ColorVision.Engine/Templates/Flow` 宿主层：流程模板如何加载、编辑、保存、导入导出，以及如何接到 `FlowEngineLib`。节点执行语义和节点基类请看 [FlowEngineLib](../../engine-components/FlowEngineLib.md)。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 流程模板列表为空 | MySQL 主表、资源表、`TemplateFlow.Load()` |
| 打开流程后节点图为空 | `SysResourceModel.Value`、`ModDetailModel.ValueA`、Base64 数据 |
| 保存后重开没变化 | `FlowEngineToolWindow.Save()` 路径、`DataBase64`、资源 `Type = 101` |
| 节点属性没有设备/模板下拉 | `STNodeEditorHelper`、`NodeConfigurator/`、服务和模板列表 |
| `.cvflow` 导入后模板名不对 | `manifest.json`、重名映射、`ReplaceTemplateNames(...)` |
| 调度能触发但结果没回去 | `FlowJob`、`RunFlowAndWaitAsync()`、`FlowCompleted`、项目包 `Processing` |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 流程模板 | `TemplateFlow` | 从数据库主表和资源表加载，管理新建、保存、删除、导入、导出 |
| 编辑窗口 | `FlowEngineToolWindow` | 独立流程编辑窗口，不是普通模板右侧编辑区 |
| 编辑器宿主 | `STNodeEditorHelper` | 托管节点画布、属性面板、节点树、剪贴板、右键菜单、合法性检查 |
| 节点配置 | `NodeConfigurator/*.cs` | 把设备列表、本地图像、普通模板、JSON 模板挂进节点属性面板 |
| 单流程包 | `.cvflow`、`FlowPackageHelper` | ZIP 包，包含 `flow.stn` 和 `manifest.json`，可携带关联模板 |
| 旧图文件 | `.stn` | 仅保存节点图原始数据 |
| 调度运行 | `FlowJob`、`DisplayFlow.RunFlowAndWaitAsync()` | Quartz 线程切回 UI 线程运行流程并等待结果 |

## 存储边界

| 场景 | 当前行为 |
| --- | --- |
| 主存储 | MySQL 主表 + 明细表 + `SysResourceModel.Value` 保存 Base64 节点图 |
| 本地 `.stn` | 打开本地文件时保存只写回文件，不更新数据库模板 |
| 数据库流程 | 保存前 `CheckFlow()`，再取画布数据、Base64、`TemplateFlow.Save2DB(...)` |
| 资源引用 | `SysResourceModel.Type = 101`，`ModDetailModel.ValueA` 保存资源 id |
| 多选导出 | 仍是 zip 内多个 `.stn`，不会像 `.cvflow` 一样收集关联模板 manifest |

## `.cvflow` 包

| 包内文件 | 作用 |
| --- | --- |
| `flow.stn` | 流程画布二进制数据 |
| `manifest.json` | `FlowPackageManifest`，记录流程名、版本和关联模板 |

导出时 `FlowPackageHelper.CollectTemplatesForExport(...)` 会扫描 STN 里的模板引用属性，如 `TempName`、`POITempName`、`SavePOITempName`、`OutputTemplateName`、`ModelName` 等，并继续扫描模板内容里的二级引用。

导入时会读取包、创建关联模板、处理重名映射、改写 STN 模板引用，再把最终 STN 转成 Base64 作为新流程模板内容。

## 运行链路

| 入口 | 当前链路 | 维护重点 |
| --- | --- | --- |
| UI 手动运行 | `DisplayFlow.RunFlow()` -> `FlowControl.Start(sn)` | 创建 `MeasureBatchModel`，绑定 `FlowCompleted` |
| 等待式运行 | `DisplayFlow.RunFlowAndWaitAsync()` | 给调度、自动化或外部调用等待流程结束 |
| Quartz 调度 | `FlowJob.Execute(...)` -> UI 线程调用 `RunFlowAndWaitAsync()` | 通过 `Application.Current.Dispatcher` 切回 UI |
| 停止流程 | `DisplayFlow.StopFlow()` -> `FlowControl.Stop()` | 批次状态更新为 `Canceled` |

`FlowControl_FlowCompleted(...)` 会写回 `FlowStatus`、`TotalTime`、`Result`，触发 `FlowExecutionCompleted`，再进入项目包后续 `Processing(FlowEngineManager.Batch)`。

## 验收

| 场景 | 必验项 |
| --- | --- |
| 保存流程 | 新增节点、选择模板、保存、关闭、重开后参数仍在 |
| 单流程导出 | `.cvflow` 包内有 `flow.stn` 和 `manifest.json` |
| 单流程导入 | 同名模板环境下能重命名冲突模板并更新节点引用 |
| 多流程导出 | zip 内是多个 `.stn`，不要误认为包含关联模板 |
| 调度执行 | Quartz `FlowJob` 能启动流程并在 `context.Result` 返回 `FlowJobResult` |
| 项目维护 | `FlowCompleted` 后批次状态、耗时、结果和项目包处理都能追踪 |

## 边界

- 本页不是 `FlowEngineLib` 重复页；这里讲主程序模板管理、窗口编辑和宿主桥接。
- 流程模板主路径是数据库 + 资源表，不是扫描磁盘目录。
- 节点属性编辑大量依赖 `NodeConfigurator` 和 `STNodeEditorHelper`。
- `.stn` 只含节点图，`.cvflow` 才包含 manifest 和关联模板。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 流程模板 | `TemplateFlow.cs` |
| 编辑窗口 | `FlowEngineToolWindow.xaml.cs` |
| 编辑器宿主 | `STNodeEditorHelper.cs` |
| 节点属性配置 | `NodeConfigurator/` |
| `.cvflow` 导入导出 | `FlowPackageHelper` 相关实现 |
