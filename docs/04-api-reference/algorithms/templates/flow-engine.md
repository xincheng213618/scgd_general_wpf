# 流程引擎

本页只描述当前仓库里 `Engine/ColorVision.Engine/Templates/Flow` 这一层的真实职责，不再继续维护“把整个 Flow 执行内核、宿主桥接、节点库都混成一页”的旧稿。

## 先看这页现在讲什么

当前这页讲的不是 `FlowEngineLib` 执行内核本身，而是主程序里围绕流程模板的宿主层，重点包括：

- 流程模板怎样从数据库和资源表加载。
- 双击流程模板后怎样打开编辑窗口。
- 编辑窗口怎样托管 `STNodeEditor`、属性面板和节点树。
- 宿主层怎样把设备、模板和节点配置器挂到流程编辑器里。

如果要看节点执行语义和节点基类，请转到 [FlowEngineLib](../../engine-components/FlowEngineLib.md)。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/*.cs`

这几处代码共同决定了主程序里的流程模板是如何编辑、保存和配置的。

## 当前主链怎么跑

### 流程模板入口

`MenuTemplateFlow` 会打开 `TemplateEditorWindow(new TemplateFlow())`。`TemplateFlow` 本身是 `ITemplate<FlowParam>` 的一个具体实现，当前负责：

- 从 MySQL 读取流程模板主表
- 把节点图内容从 `SysResourceModel.Value` 取回成 Base64
- 将其包装进 `FlowParam`
- 管理保存、删除、导入、导出和新建

因此当前流程模板不是单纯的磁盘文件列表，而是“数据库主记录 + 资源表二进制内容”的组合。

### 双击后的编辑窗口

`TemplateFlow.PreviewMouseDoubleClick(...)` 会直接打开 `FlowEngineToolWindow`。这说明流程模板和很多普通模板不同：

- 列表窗口只是入口
- 真正的流程编辑发生在独立窗口里

窗口里再通过 `STNodeEditorHelper` 托管节点画布、属性面板、节点树、剪贴板和右键菜单。

### 编辑器辅助层

`STNodeEditorHelper` 当前负责的事情很多，远不止“帮忙调一下节点树”：

- 节点复制和粘贴的压缩序列化
- 当前选中节点与属性面板同步
- 节点树初始化和装配
- 右键菜单、删除、全选等命令
- 合法性检查和自动布局
- 设备与模板选择面板的宿主挂接

这意味着流程编辑窗口的大量交互逻辑都集中在这个 helper 里，而不是散落在每个节点控件中。

### 节点配置器桥接

`NodeConfigurator` 目录当前是主程序和节点库之间的重要桥接层。这里会把：

- 设备服务列表
- 本地图像路径输入
- 普通模板选择器
- JSON 模板选择器

装进节点属性面板。

例如 POI 相关配置器会把 `TemplatePoi`、`TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` 等模板接回流程节点。也就是说，节点在宿主里的可编辑体验，并不完全由 `FlowEngineLib` 决定。

## 当前存储与导出边界

### 主存储仍是数据库

`TemplateFlow.Load()` 和 `Save2DB(...)` 当前都围绕 MySQL 主表、明细表以及 `SysResourceModel` 展开。Base64 节点图内容会落到资源表，再通过明细记录关联回来。

### 导出不只是一种格式

当前流程模板导出至少有两种实际形式：

- `.stn`：节点图原始文件
- `.cvflow`：带关联模板信息的流程包

因此把流程模板简单写成“只是一张节点图文件”，会漏掉当前包导出的能力。

### 保存路径要分清数据库和本地文件

当前 `FlowEngineToolWindow.Save()` 有两条路径：

| 场景 | 行为 | 交接重点 |
| --- | --- | --- |
| 打开的是本地 `.stn` 文件 | `SaveToFile(FileFlow)` 直接写回文件 | 不会更新数据库模板，也不会更新资源表。 |
| 打开的是 `FlowParam` 模板 | 先 `STNodeEditorHelper.CheckFlow()`，再 `GetCanvasData()`、Base64、`TemplateFlow.Save2DB(...)` | 会更新 `ModMasterModel.Name`、`SysResourceModel.Value` 和明细里的资源引用。 |

`TemplateFlow.Save2DB(...)` 会把画布数据保存到 `SysResourceModel`，资源 `Type = 101`，`ModDetailModel.ValueA` 保存资源 id。交接时如果“保存成功但重新打开没变化”，优先查 `DataBase64` 是否变化、`ValueA` 是否指向正确资源、资源表里的 `Value` 是否更新。

### .cvflow 包结构

`.cvflow` 当前不是简单改后缀的 `.stn`，而是 ZIP 包：

| 包内文件 | 作用 |
| --- | --- |
| `flow.stn` | 流程画布二进制数据 |
| `manifest.json` | `FlowPackageManifest`，记录流程名、版本和关联模板 |

`FlowPackageHelper.CollectTemplatesForExport(...)` 会从 STN 数据里扫描模板引用属性，例如 `TempName`、`POITempName`、`SavePOITempName`、`OutputTemplateName`、`ModelName` 等，再把引用到的模板一起打进 manifest。它还会继续扫描模板序列化内容里引用的其它模板，所以导出的包可能包含一层以上的模板依赖。

导入 `.cvflow` 时，`ImportFlowPackage(...)` 会：

1. 读取 `flow.stn` 和 `manifest.json`。
2. 调用 `FlowPackageHelper.ImportTemplates(...)` 创建关联模板。
3. 如果模板重名，生成新名称并记录旧名到新名的映射。
4. 调用 `ReplaceTemplateNames(...)` 改写 STN 里的模板引用。
5. 把最终 STN 转成 Base64，作为新流程模板的导入内容。

因此排查“导入后模板名对不上”时，要看 manifest、重名映射和 STN 引用替换，而不是只看流程模板名称。

### 多选导出仍是旧 zip 语义

当一次选择多个流程模板导出时，当前代码会把每个流程写成独立 `.stn` 文件并打成 zip。这个路径不会像单个 `.cvflow` 一样收集关联模板 manifest。用于现场迁移时，优先使用单个 `.cvflow` 验证依赖模板是否完整。

## 运行与调度链路

流程模板的执行入口不只有“用户点运行”。

| 入口 | 当前链路 | 交接重点 |
| --- | --- | --- |
| UI 手动运行 | `DisplayFlow.RunFlow()` -> `FlowControl.Start(sn)` | 会创建 `MeasureBatchModel`，并绑定 `FlowCompleted`。 |
| 等待式运行 | `DisplayFlow.RunFlowAndWaitAsync()` | 给调度、自动化或外部调用等待流程结束。 |
| Quartz 调度 | `FlowJob.Execute(...)` -> UI 线程调用 `RunFlowAndWaitAsync()` | 调度线程不能直接操作 WPF UI，当前通过 `Application.Current.Dispatcher` 切回 UI 线程。 |
| 停止流程 | `DisplayFlow.StopFlow()` -> `FlowControl.Stop()` | 会把批次状态更新为 `Canceled`。 |

`FlowControl_FlowCompleted(...)` 会把 `FlowControlData` 写回当前批次：

- `FlowStatus`
- `TotalTime`
- `Result`

随后触发 `FlowExecutionCompleted`，并进入后续 `Processing(FlowEngineManager.Batch)`。如果现场反馈“流程跑完但项目包没接到结果”，要从 `FlowCompleted`、批次更新、项目包 `Processing` 三段连续排查。

## 当前几个最容易写错的点

### 这页不是 FlowEngineLib 重复页

`FlowEngineLib` 负责节点执行与基类体系；本页这层负责主程序里的模板管理、窗口编辑和宿主桥接。两层都叫“流程引擎”，但边界不同。

### 流程模板不是纯磁盘资产

当前主路径仍然是数据库 + 资源表，不是扫描某个目录里的 `.stn` 文件。导入导出只是附加能力。

### 节点属性编辑大量依赖宿主代码

真正把设备下拉框、模板下拉框、JSON 模板下拉框挂进节点属性区的，是 `NodeConfigurator` 和 `STNodeEditorHelper` 这一层，而不只是节点类本身。

### 窗口行为和一般模板编辑器不同

普通模板多半在 `TemplateEditorWindow` 右侧编辑；流程模板当前走的是“列表窗口 + 独立流程编辑器窗口”的路径。继续沿用一般模板的叙述会误导读者。

### 导入导出有两套兼容路径

`.stn` 只包含节点图，`.cvflow` 才包含 manifest 和关联模板。多选 zip 仍偏旧语义，只打包多个 `.stn`。交接、备份和跨机迁移时要优先确认实际用的是哪种格式。

## 验收建议

| 场景 | 必验项 |
| --- | --- |
| 保存流程 | 新增节点、选择模板、保存、关闭窗口、重新打开后参数仍在 |
| 单流程导出 | 导出 `.cvflow`，确认包内有 `flow.stn` 和 `manifest.json` |
| 单流程导入 | 导入到已有同名模板环境，确认冲突模板被重命名且节点引用已更新 |
| 多流程导出 | 确认 zip 内是多个 `.stn`，不要误认为包含关联模板 |
| 调度执行 | Quartz `FlowJob` 能启动流程并在 `context.Result` 返回 `FlowJobResult` |
| 项目交接 | `FlowCompleted` 后批次状态、耗时、结果和项目包处理都能追踪 |

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
2. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator` 下其它配置器

## 继续阅读

- [FlowEngineLib](../../engine-components/FlowEngineLib.md)
- [Flow 节点扩展](../../extensions/flow-node.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
