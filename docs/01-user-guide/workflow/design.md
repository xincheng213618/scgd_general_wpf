---
knowledge_id: "flow.workspace"
knowledge_type: "topic"
status: "current"
summary: "流程编辑器的打开与保存步骤、导出/删除范围、切换提示和工作区隔离；区分当前画布与已保存模板。"
aliases: ["流程设计","拖节点","节点参数","保存流程","ViewFlow","FlowEditorCanvas","ActiveFlowParam","FlowTemplateWorkspaceController","流程编辑器","流程引擎模板管理","导入模板为模块","自动对齐","适应全部节点"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowEngineManager.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowTemplateWorkspaceController.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorCanvas.xaml","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorCanvas.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEngineToolWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorOperations.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowNodeContextMenuService.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/TemplateSelectionDialog.xaml","Engine/ColorVision.Engine/FlowProcessing/Editor/TemplateSelectionDialog.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowTemplateWorkspaceControllerTests.cs","Test/ColorVision.UI.Tests/ViewFlowDocumentBehaviorTests.cs","Test/ColorVision.UI.Tests/FlowLocalShortcutTests.cs","Test/ColorVision.UI.Tests/STNodeCopyPasteTests.cs"]
related: ["flow.architecture","flow.editor","flow.templates","flow.session","flow.headless","ui.property-grid"]
---

# 流程编辑器：画布与工作区文档命令

使用流程编辑器创建节点图、配置参数、保存流程或打开本地画布。先确认编辑对象是数据库模板还是本地文件，再执行保存、刷新和导出。

`ViewFlow` 组合 `FlowEditorCanvas`、模板工作区和执行会话；独立窗口 `FlowEngineToolWindow` 复用同一个 `ViewFlow`。底层节点与画布输入见 [ST.Library.UI](../../04-api-reference/engine-components/ST.Library.UI.md)，数据库和包格式见[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)。

编辑、导入模块和布局改变当前文档；保存或删除数据库模板会改持久数据，导入流程包还可能创建关联模板。切换文档可能触发保存提示或结束当前运行。必须确认目标文档、备份和已授权动作，不能用实际运行设备来代替只读排查。

## 打开并编辑流程

1. 从“模板 > 流程”打开“流程引擎模板管理”，双击目标流程进入编辑器。“工作流程”区域的齿轮也可打开模板管理。通过 `.stn` / `.cvflow` 文件打开的窗口则使用文件文档模式。
2. 在画布空白处右击添加节点，或选择“导入模板为模块”插入已有流程的节点图。选中节点后编辑属性，再核对端口连接、设备 Code 和模板绑定。
3. 用“自动对齐”（画布内 Ctrl+L）整理位置，用“适应全部节点”查看完整图。布局和视口调整不会代替保存。
4. 核对窗口标题及下表中的保存目标，点击“保存”。切换模板或刷新时，若画布有未保存修改，会询问保存、放弃或取消；选择保存时，需保存成功才继续。导出前仍需自行确认保存成功。

编辑完成后要运行当前画布，使用同一编辑器工具栏中工具提示为“执行流程”的按钮。“工作流程”面板也提供同名按钮。启动前提、停止行为和最终状态见[流程执行](./execution.md)；保存流程与执行流程是两个命令，执行不会替代保存。

ARVRPro、LUX、KB 的流程编辑按钮在未选择模板时仍可打开空白独立文档；列表为空或原选择索引已失效时也采用此方式，不自动选取其它流程。窗口没有关联模板时，模板删除和版本历史等需要选中模板的操作不可用；之后仍可新建、打开文件或编辑画布，保存按独立文档规则选择文件目标。

## 保存目标与命令范围

共享主工作区与独立窗口复用同一套 `ViewFlow` 命令，内部按宿主模式分流：

| 命令 | 主程序模板工作区 | 独立窗口 |
| --- | --- | --- |
| 新建 | `NewFlow` 创建模板 | `NewDocument` 新建本地文档 |
| 导入/打开 | `ImportFlow` 经 `TemplateFlow` 导入并创建流程模板后刷新；取消导入不刷新当前画布 | `OpenDocument` / `OpenStandaloneFile` 打开 `.stn` 或读取 `.cvflow` 画布 |
| 保存 | `TrySave` 校验画布后写入当前已加载模板 | `SaveStandaloneDocument`：本地文件写回文件；明确以可保存模板模式打开的 `FlowParam` 才写数据库 |
| 导出 | 从已加载模板取得索引后调用 `TemplateFlow.Export`；最终对象受勾选项影响，且不先保存画布 | 调用 `SaveStandaloneDocument`，不能据按钮名称推断一定导出完整 `.cvflow` 包 |
| 删除 | 确认框显示已加载模板名；实际删除范围仍受共享勾选项影响 | 模板删除命令不可用 |
| 刷新 | 确认未保存修改后重载请求选择的模板；活动运行会先收到取消请求并等待收尾 | 对已有文件/模板执行文档替换确认后重载 |

导出与删除的最终对象由[模板勾选规则](../../04-api-reference/engine-components/template-flow-chain.md#导出与删除的目标选择)决定。操作前检查并清除不需要的勾选，不能仅凭当前画布或确认框名称确定范围。主工作区导出读取模板的 `DataBase64`，未保存的节点修改不会自动写入导出包。

`TrySave` / `SaveStandaloneDocument` 都先通过 `FlowValidator.Validate` 并取得非空画布数据。数据库保存携带窗口自己的 `FlowTemplateSaveCondition(_documentLoadedContentHash)`；失败会恢复内存中的旧 `DataBase64`，成功后才更新文档基线并 `MarkSaved`。数据库并发保存及包兼容的权威事实见[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)。

保存校验要求至少有一条从任意起始节点到结束节点或“流程终止”节点的执行路径，不受节点点击顺序或绘制层级影响；其它尚未连接完成的节点、起点或分支可以保留。循环控制线和服务数据线不视为执行完成路径，旧循环和中继节点的正常执行端口仍可通过。重复框自身仍须完整有效；若没有执行起止路径且只有一个起点，提示前会定位到该起点，存在多个起点时不猜测。这是保存的最低结构检查，不代表所有分支、参数和设备都能成功运行。

打开本地 `.stn` 后保存只写回该文件，不更新数据库模板。打开 `.cvflow` 作为独立文档时读取包内画布，不等于执行主程序的完整模板导入流程；首次保存可落成 `.stn`。不要把“独立窗口”直接等同于“永不写数据库”：`FlowEngineToolWindow(FlowParam)` 使用允许模板保存的打开模式。

## 画布编辑能力

| 能力 | 实现落点与约束 |
| --- | --- |
| 添加节点、连接端口、拖动和命名 | `FlowEditorCanvas` 承载 `STNodeEditor`；节点目录与端口兼容由 ST 库负责。空白处右键新建菜单按最终显示分类合并，分类内优先按节点顺序排列，同序号再按名称自然排序，Engine 与 FlowEngineLib 的“02 相机”等同名分类共用一个入口；第三方程序集前缀保留 |
| 画布平移与框选 | 每次打开流程时工具栏锁按钮默认开启，空白处左键拖动平移画布；首次选中节点后一次性进入编辑模式并保持，清空或重新选择节点都不会自动切回。编辑模式下普通左键拖动框选，Ctrl + 左键拖动或中键拖动可临时平移；按钮仍可手动切换两种模式 |
| 节点参数 | 单选活动节点时显示其属性面板；框选或 Ctrl+A 多选后隐藏单节点面板，恢复单选后按当前选集显示。属性标注接入统一 PropertyGrid；设备 Code、模板选择与输入字段须分别确认，连线成功不代表参数正确 |
| 撤销/重做、复制/粘贴、删除 | Canvas 转发编辑命令到 ST 控件的历史栈；右键已选中的节点时复制、删除整个选集，右键未选中的节点时只处理该节点，整组选集一次撤销即可恢复。只读画布禁用右键编辑、创建与模块导入；菜单打开后进入只读也不能继续修改。撤销不等于撤销已经写入数据库或外部系统的动作 |
| 重复框 | 框选一个节点或连续的一段，右键“重复执行…”设置总次数（包含第一次）。框内整段按原顺序执行完才进入下一轮，中间结果覆盖，只向框外传出最后一轮的结果；相机单独重复 10 次时，后面的算法仅运行一次 |
| 自动布局 | `ViewFlow.AutoAlignment` 调用布局服务整理节点位置；画布局部快捷键仅精确匹配 Ctrl+L，不接受额外 Alt/Shift/Win，避免误接管日志的 Ctrl+Alt+L |
| 自动适配 | `AutoSizeCommand` 调用 `FitToViewport` 调整视口，不是保存操作 |
| 导入模块 | 工具栏命令和画布空白处右键“导入模板为模块”复用 `ImportModule`：点击后重新加载模板并打开 `TemplateSelectionDialog`，选择画布后交给 `FlowEditorOperations.ImportCanvasAsModule` 加入当前图；没有模板、模板无画布数据或导入异常时显示提示；随后仍需检查参数并保存当前文档 |

重复框允许多个，每框独立设置次数，成员固定；移动节点不改变归属。双击框标题可修改次数，拖动标题会一起移动框内节点，整次拖动可一次撤销；若框内有位置锁定的节点，整框不移动。右键框内节点或边框区域也可“修改重复次数…”或“取消重复框”，取消保留节点和连线。创建、修改、取消可撤销，框随画布保存；整框复制生成独立的新框，部分复制只保留节点。第一版不支持分支、嵌套、重叠成员及包含开始、结束、旧循环节点的框。删除成员或改变连接后，须重新框选有效的一段；不会静默改成另一段继续执行。

运行中框标题显示当前轮次，完成后保留完成次数；停止、失败或超时保留发生时的轮次，超时按失败显示。边框颜色辅助区分状态，文字仍明确说明结果。后续节点失败不改写先前已经完成的框；从同一起点接受新运行时清除可达框的旧状态。运行状态不保存进模板，也不产生未保存修改。

每轮从进入该框时的上游输入开始，框内按连接顺序执行；多个框串联时，前框全部完成才进入后框，不将次数相乘。异常、取消、超时沿用原流程状态与清理链，停止后不再启动下一轮。这里的覆盖指流程上下文与传给后续节点的结果；节点既有的采集保存、数据库记录和执行诊断策略不因重复框改变。本地图像与 POI 的中间轮次运行资源会在换轮时释放，上游输入及最后一轮资源保留到原有生命周期结束。

实现入口为 `FlowNodeContextMenuService.Repeat.cs`、`FlowEngineLib/Repeat/FlowRepeatRegion.cs` 和 `STNodeEditor.Groups.cs`。`FlowRepeatRegionTests` 保护最终结果只放行一次、多框顺序、停止与资源回收，以及画布保存、撤销、复制边界；不代替真实相机或远程算法服务验收。

普通设备/模板字段使用 属性上的 `PropertyEditorTypeAttribute`；多模板族、随算法类型变化的补充面板归 `Editor/NodeConfiguration/`。选择顺序、缓存和降级规则只在 [PropertyGrid 契约](../../04-api-reference/ui-components/property-grid.md)维护。

## 选择、加载与多窗口隔离

`FlowTemplateWorkspaceController` 区分 requested template 和已加载的 `ActiveFlowParam`。请求选择还在加载时，保存使用当前画布的 active 模板，不能把画布写进刚选中但尚未加载的模板；历史查询可以跟随 requested 选择。导出和删除入口也从 active 模板取得索引，但底层勾选规则仍可能改变最终范围。

模板工作区刷新使用 generation 和 latest-wins 门禁串行加载，旧请求不能覆盖新选择。加载失败会尝试恢复前一画布与选择；失败的新请求不能冒用旧模板快照继续执行。独立窗口使用自己的工作区/服务节点集合，不写主程序的全局模板选择和全局节点集合。

主工作区通过下拉框切换或点击刷新前，若画布有未保存修改，会询问保存、放弃或取消。选择保存后只有保存成功才继续；取消或保存失败保留当前画布、未保存状态、撤销/重做历史和保存基线，并恢复当前模板选择。确认期间首次保存无关联模板的画布时，先建立其保存目标，再继续原来的切换请求；若期间出现更新的选择，只允许最新请求加载。新建流程已经确认过文档替换，不重复询问。

关闭模板管理时先同步模板列表，不无条件重载画布。当前模板仍存在且 ID、已保存画布 `DataBase64` 未变时，改名、版本信息变化或列表重新排序只更新当前模板对象和选择索引，保留未保存修改、撤销/重做历史、执行摘要和视口，也不停止运行。只有已保存画布内容变化才进入上述替换确认与重载流程；已经有更新的选择请求等待加载时，由该请求继续处理，不因关闭模板管理而取消。

若当前已加载的列表模板被删除，先解除其保存目标和保存基线，再处理后继模板选择。此时取消未保存修改提示会保留当前画布和历史，成为无关联草稿，之后可保存为新模板；干净画布可切换到列表中剩余模板，列表为空则清空。已有待加载请求仍继续，不另发请求覆盖它。仅以“原来属于模板列表、同步后已不存在”判定删除，显式加载但从未注册到列表的模板不会被误判为已删除。

显式程序调用 `SelectFlowTemplateAsync` 或指定模板的运行接口不弹保存对话框；当前画布有未保存修改时直接拒绝切换并返回失败，保留当前文档。调用方应先处理保存或放弃修改，不能把切换失败当作已选中目标。界面完成导入后的刷新同样经过替换确认；取消导入或导入对话框返回时已不允许编辑，不创建流程模板，也不刷新画布。刷新还会取消活动运行并等待收尾，不能当作单纯更新状态显示。

独立窗口的新建、打开、刷新和关闭经过 `ConfirmDocumentReplacement`；选择保存时只有保存成功才继续，选择取消则保留当前文档。独立文件/模板打开先读取并解析目标，成功后才替换画布与保存目标；文件缺失、无效 Base64 或损坏画布不会清空当前节点，也不会丢失文档身份、未保存修改和撤销/重做历史。选择放弃修改后若目标读取或解析失败，当前文档仍保留。加载后的设备绑定和实际执行需要另外验证。

## 当前画布与已保存版本

UI 手动运行及未指定模板的运行接口读取当前画布，可执行尚未保存的编辑。指定模板的运行接口先选择模板，只使用本次成功选择返回的执行快照；选择失败、被更新的选择替代或快照在启动前失效时返回失败，不启动旧图或其它模板。共享 Quartz `FlowJob` 经单例 `FlowEngineManager.View` 的主工作区运行并等待最终化，不是运行当前激活的任意独立窗口。`HeadlessFlowJob` / `RunSavedFlowHeadlessAsync` 按 `FlowKey` 取模板集合中的 `DataBase64`，不读取当前未保存画布。不能笼统声称“所有 Quartz 都只运行已保存版本”。

因此启用读取已保存模板的入口前，应保存并确认 `FlowKey`、起始节点和实际版本；无界面请求创建后的字节副本不再随继续编辑变化。执行完成判据见[执行会话](./execution.md)，无界面边界见[隔离执行](../../04-api-reference/algorithms/templates/flow-engine.md)。

## 故障定位与验证

| 现象 | 第一检查点 |
| --- | --- |
| 选中模板但画布为空 | requested/active 是否一致、模板是否有 `DataBase64`、加载是否失败；新空模板不能当作已完成流程 |
| 保存后重开没有变化 | 文件文档还是数据库模板、保存返回值、窗口内容 hash、最终持久化对象 |
| 导出缺少刚编辑的节点 | 主工作区导出不先保存，先确认模板保存成功，再核对导出对象及包内画布 |
| 刷新或切换被取消 | 是否选择取消、保存校验或写入失败；程序化切换是否遇到未保存修改。此时应保留原画布与历史，先处理修改再重试 |
| 改了画布但调度像没变 | 区分 `FlowJob` 与 `HeadlessFlowJob`，核对当前画布和已保存版本 |
| 快速切模板显示错对象 | generation、latest-wins 和失败恢复，不只看下拉框文字 |
| 图能运行但参数不对 | 节点属性、设备/模板绑定及输入来源；再进入[执行诊断](./execution.md) |
| 右键“导入模板为模块”无响应 | 入口应是直接点击项并打开模板选择窗口；检查模板重新加载、窗口 Owner，以及无模板、空画布数据和导入异常的提示 |

`FlowTemplateWorkspaceControllerTests` 覆盖选择、起点、刷新门禁与快照身份；`ViewFlowDocumentBehaviorTests` 覆盖命令分流与修改文档替换决定；`STNodeCopyPasteTests` 实际覆盖节点保存/加载、类型重定位和损坏画布不替换旧图，不能仅凭文件名声称模块导入 UI 已验证。这些测试不证明完整 WPF 保存交互或真实 MySQL 已验收。需要相应验证时，检查新增节点/参数保存重开、模块插入、快速 A→B 选择、坏模板失败恢复、独立窗口不污染主窗口，以及取消/保存失败不丢文档。

`FlowLocalShortcutTests` 只测试自动排列的纯键位判断，覆盖修饰键组合和非目标主键；不构造 `ViewFlow`、不移动节点，也不代替真实键盘与排列结果验收。
