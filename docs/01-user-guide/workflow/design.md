---
knowledge_id: "flow.workspace"
knowledge_type: "topic"
status: "current"
summary: "ViewFlow 与 FlowEditorCanvas 的编辑命令、文档目标、工作区隔离及未保存画布的执行边界。"
aliases: ["流程设计","拖节点","节点参数","保存流程","ViewFlow","FlowEditorCanvas","ActiveFlowParam","FlowTemplateWorkspaceController"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowTemplateWorkspaceController.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorCanvas.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEngineToolWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorOperations.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowTemplateWorkspaceControllerTests.cs","Test/ColorVision.UI.Tests/ViewFlowDocumentBehaviorTests.cs","Test/ColorVision.UI.Tests/STNodeCopyPasteTests.cs"]
related: ["flow.architecture","flow.editor","flow.templates","flow.session","flow.headless","ui.property-grid"]
---

# Flow 编辑工作区与文档命令

`ViewFlow` 组合 `FlowEditorCanvas`、模板工作区和执行会话；`FlowEngineToolWindow` 只是独立窗口宿主，复用同一个 `ViewFlow`。底层节点、端口和画布输入属于 [ST.Library.UI](../../04-api-reference/engine-components/ST.Library.UI.md)，数据库和包格式属于[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)。

编辑、导入模块和布局改变当前文档；保存或删除数据库模板会改持久数据，导入流程包还可能创建关联模板。切换文档可能触发保存提示或结束当前运行。必须确认目标文档、备份和已授权动作，不能用实际运行设备来代替只读排查。

## 相同按钮不代表相同保存目标

主窗口和独立窗口共用工具栏，命令内部按宿主模式分流：

| 命令 | 主程序模板工作区 | 独立窗口 |
| --- | --- | --- |
| 新建 | `NewFlow` 创建模板 | `NewDocument` 新建本地文档 |
| 导入/打开 | `ImportFlow` 经 `TemplateFlow` 导入并创建流程模板 | `OpenDocument` / `OpenStandaloneFile` 打开 `.stn` 或读取 `.cvflow` 画布 |
| 保存 | `TrySave` 校验画布后写入当前已加载模板 | `SaveStandaloneDocument`：本地文件写回文件；明确以可保存模板模式打开的 `FlowParam` 才写数据库 |
| 导出 | `ExportFlow` 导出当前已加载模板 | 调用 `SaveStandaloneDocument`，不能据按钮名称推断一定导出完整 `.cvflow` 包 |
| 删除 | 对当前已加载模板确认后删除 | 模板删除命令不可用 |

`TrySave` / `SaveStandaloneDocument` 都先通过 `FlowValidator.Validate` 并取得非空画布数据。数据库保存携带窗口自己的 `FlowTemplateSaveCondition(_documentLoadedContentHash)`；失败会恢复内存中的旧 `DataBase64`，成功后才更新文档基线并 `MarkSaved`。数据库并发保存及包兼容的权威事实见[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)。

打开本地 `.stn` 后保存只写回该文件，不更新数据库模板。打开 `.cvflow` 作为独立文档时读取包内画布，不等于执行主程序的完整模板导入流程；首次保存可落成 `.stn`。不要把“独立窗口”直接等同于“永不写数据库”：`FlowEngineToolWindow(FlowParam)` 使用允许模板保存的打开模式。

## 画布编辑能力

| 能力 | 实现落点与约束 |
| --- | --- |
| 添加节点、连接端口、拖动和命名 | `FlowEditorCanvas` 承载 `STNodeEditor`；节点目录与端口兼容由 ST 库负责 |
| 节点参数 | 属性标注接入统一 PropertyGrid；设备 Code、模板选择与输入字段须分别确认，连线成功不代表参数正确 |
| 撤销/重做、复制/粘贴、删除 | Canvas 转发编辑命令到 ST 控件的历史栈；撤销不等于撤销已经写入数据库或外部系统的动作 |
| 自动布局 | `ViewFlow.AutoAlignment` 调用布局服务整理节点位置 |
| 自动适配 | `AutoSizeCommand` 调用 `FitToViewport` 调整视口，不是保存操作 |
| 导入模块 | `ImportModule` 选择已有流程模板的画布，交给 `FlowEditorOperations.ImportCanvasAsModule` 加入当前图；随后仍需检查参数并保存当前文档 |

普通设备/模板字段使用 `FlowNodePropertyEditorAttribute` 或 `PropertyEditorTypeAttribute`；多模板族、随算法类型变化的补充面板归 `Editor/NodeConfiguration/`。选择顺序、缓存和降级规则只在 [PropertyGrid 契约](../../04-api-reference/ui-components/property-grid.md)维护。

## 选择、加载与多窗口隔离

`FlowTemplateWorkspaceController` 区分 requested template 和已加载的 `ActiveFlowParam`。请求选择还在加载时，保存、删除和导出必须针对当前画布的 active 模板，不能把画布写进刚选中但尚未加载的模板；历史查询可以跟随 requested 选择。

刷新使用 generation 和 latest-wins 门禁串行加载，旧请求不能覆盖新选择。加载失败会尝试恢复前一画布与选择；失败的新请求不能冒用旧模板快照继续执行。独立窗口使用自己的工作区/服务节点集合，不写主程序的全局模板选择和全局节点集合。

独立窗口的文档替换/关闭经过 `ConfirmDocumentReplacement`；保存、丢弃和取消是不同决定。取消替换应保留当前文档，保存失败不能被当成已同意丢弃。

## 当前画布与已保存版本

UI 手动运行读取当前画布，可执行尚未保存的编辑；共享 Quartz `FlowJob` 经单例 `FlowEngineManager.View` 的主工作区运行并等待最终化，不是运行当前激活的任意独立窗口。`HeadlessFlowJob` / `RunSavedFlowHeadlessAsync` 按 `FlowKey` 取模板集合中的 `DataBase64`，不读取当前未保存画布。不能笼统声称“所有 Quartz 都只运行已保存版本”。

因此启用读取已保存模板的入口前，应保存并确认 `FlowKey`、起始节点和实际版本；无界面请求创建后的字节副本不再随继续编辑变化。执行完成判据见[执行会话](./execution.md)，无界面边界见[隔离执行](../../04-api-reference/algorithms/templates/flow-engine.md)。

## 故障定位与验证

| 现象 | 第一检查点 |
| --- | --- |
| 选中模板但画布为空 | requested/active 是否一致、模板是否有 `DataBase64`、加载是否失败；新空模板不能当作已完成流程 |
| 保存后重开没有变化 | 文件文档还是数据库模板、保存返回值、窗口内容 hash、最终持久化对象 |
| 改了画布但调度像没变 | 区分 `FlowJob` 与 `HeadlessFlowJob`，核对当前画布和已保存版本 |
| 快速切模板显示错对象 | generation、latest-wins 和失败恢复，不只看下拉框文字 |
| 图能运行但参数不对 | 节点属性、设备/模板绑定及输入来源；再进入[执行诊断](./execution.md) |

`FlowTemplateWorkspaceControllerTests` 覆盖选择、起点、刷新门禁与快照身份；`ViewFlowDocumentBehaviorTests` 覆盖命令分流与修改文档替换决定；`STNodeCopyPasteTests` 实际覆盖节点保存/加载、类型重定位和损坏画布不替换旧图，不能仅凭文件名声称模块导入 UI 已验证。这些测试不证明完整 WPF 保存交互或真实 MySQL 已验收。需要相应验证时，检查新增节点/参数保存重开、模块插入、快速 A→B 选择、坏模板失败恢复、独立窗口不污染主窗口，以及取消/保存失败不丢文档。
