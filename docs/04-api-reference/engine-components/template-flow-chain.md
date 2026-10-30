---
knowledge_id: "flow.templates"
knowledge_type: "topic"
status: "current"
summary: "Flow 本地 SQLite 与 MySQL 配置存储、保存基线、导出/删除勾选范围、cvflow v3 包兼容，以及版本/搜索侧车的失败边界。"
aliases: ["Flow模板保存后参数为什么丢失","TemplateFlow","FlowPackageHelper","cvflow","FlowKey","FlowTemplateSaveCondition","FlowTemplateConcurrencyException","导入流程","关联模板","流程删除范围","流程多选导出","模板勾选项","StnV1NeutralCodec","动态端口索引","逻辑与索引警告","本地流程","离线流程模板","LocalFlowTemplateStorage","流程封面","流程平铺","FlowTemplateManagerWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","Engine/ColorVision.Engine/Templates/Flow/LocalFlowTemplateStorage.cs","Engine/ColorVision.Engine/Templates/Flow/FlowParam.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateSaveCondition.cs","Engine/ColorVision.Engine/Templates/Flow/FlowPackageHelper.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateManagerWindow.cs","Engine/ColorVision.Engine/Templates/Browser","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateCover.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateCoverService.cs","Engine/ColorVision.Engine/Templates/Flow/Versioning","Engine/ColorVision.Engine/FlowProcessing/Compilation/FlowCanvasCatalogBuilder.cs","Engine/ColorVision.Engine/FlowProcessing/Compilation/StnV1NeutralCodec.cs"]
test_paths: ["Test/ColorVision.UI.Tests/StartupFlowTemplateLoadingTests.cs","Test/ColorVision.UI.Tests/LocalFlowTemplateStorageTests.cs","Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs","Test/ColorVision.UI.Tests/FlowTemplateIdentityTests.cs","Test/ColorVision.UI.Tests/FlowCatalogServiceTests.cs","Test/ColorVision.UI.Tests/FlowTemplateCoverTests.cs","Test/ColorVision.UI.Tests/FlowTemplateBrowserOrderTests.cs"]
related: ["flow.architecture","flow.workspace","flow.session","flow.headless","engine.template-design","engine.results"]
---

# Flow 模板、持久化与流程包

`TemplateFlow` 管理 MySQL 或本地 SQLite 中的流程定义；`FlowPackageHelper` 负责 `.cvflow` 及其关联模板。它们不拥有画布交互、节点运行或客户最终判定。编辑对象与文件/数据库保存目标见[工作区契约](../../01-user-guide/workflow/design.md)，执行与后处理见[执行会话](../../01-user-guide/workflow/execution.md)。

导入会创建或复用关联模板，保存/删除会修改数据库。查询格式或验证文档不授权实际导入、删除或覆盖生产模板；导出可能包含客户参数，分享前须确认范围并脱敏。

## 模板发现与主存储

模板初始化与注册由[模板核心契约](../../03-architecture/components/templates/design.md)维护。列表为空先检查数据库与程序集发现，不先改菜单；以下仅描述 Flow 自己的存储、身份与包行为。

MySQL 未连接时，启动仍加载本地流程；MySQL 流程列表查询失败也会回退到本地。`LocalFlowTemplateStorage` 使用当前用户 `ApplicationData/ColorVision/Config/ColorVision.Local.db` 的 `local_templates` 表，`kind=flow`，每条记录直接保存名称、稳定 `local-flow:<guid>` 身份和完整 Base64 STN，不创建 MySQL 的主表、明细或资源关系，不保存执行结果。管理器标题标识“本地”或“MySQL”；两库独立，不缓存或同步服务器流程，首次使用本地空库需新建或导入 `.stn`。

启动通过可选的 `IAsyncTemplateLoad.LoadAsync` 读取 Flow：数据库、本地存储和版本查询在后台运行，尚未绑定的参数准备完成后回到 UI 线程更新共享集合。旧模板加载器继续调用同步 `Load()`，按约 32 ms 时间片在加载器之间让出 Dispatcher；运行期间的 MySQL 重连仍按原有同步顺序先发布模板、再更新服务资源。启动尚未完成时收到重载请求会合并并补做一轮，避免两个批次同时发布。

本地支持新建、编辑保存、重命名、复制、删除、排序及 `.stn` 导入导出；多选导出仍为多个 STN 的 ZIP。当前本地管理入口不导入带关联模板的 `.cvflow` 包，也不保证服务节点或其他 MySQL 模板可离线执行。本地 ID 小于 -1，复制生成新 FlowKey，排序保留身份。已经打开的本地流程在重新联网后仍保存到原本地库；服务器流程断线保存会报错，不自动变成本地流程。格式版本不支持或内容损坏时拒绝读取，不能覆盖为空流程。

流程下拉框为空时，齿轮仍打开模板管理，新建按钮可创建并选中第一个流程。主画布尚未关联任何模板时，首次保存要求填写名称，将当前画布写入当前配置存储并关联新模板；取消保留画布、不创建记录。若已有模板选择仍在加载，不能把旧画布作为该模板的新内容保存。

工作流程齿轮（包括共用命令的画布入口）、“模板 → 流程”菜单、ProjectARVRPro、ProjectKB 和 Conoscope 的流程模板管理使用专用 `FlowTemplateManagerWindow`；窗口与 POI 图标浏览器共用 `TemplateBrowserWindow` 的浏览交互，封面仍由流程专用组件提供。窗口采用文件管理器式紧凑工具栏：“查看”提供小、中、大、超大四档图标及列表模式，小图标横排名称，其余尺寸在封面下方最多显示两行名称；列表显示勾选框、序号、名称和日期。默认使用中等图标、保存顺序倒序，不分组。顶部提供新建、导入、导出与删除图标，Delete 删除、F2 重命名、Ctrl+F 搜索，编辑文本时 Delete 保留文本编辑语义。支持 Ctrl/Shift 多选、Ctrl+A 选择可见项和空白处拖框；高亮与勾选同步，筛选隐藏的勾选仍保留为明确的批量操作目标，删除前确认实际名称。空白处右键提供查看、排序、分组、刷新与新建；模板右键提供打开、重命名、复制、导出和删除。

“排序”支持模板保存顺序、名称自然排序及日期，均可切换升降序；“分组依据”支持名称、日期和不分组，组标题可折叠。名称按 0–9、A–H、I–P、Q–Z、中文及其他分段；日期按今天、昨天、本周、上周、本月、上月、今年及更早等日历范围分段。日期取 MySQL 主记录的 create_date 或本地文档记录的 UTC 创建时间（按本机时区显示）；旧本地文档没有日期时显示“日期未知”，日期排序始终将未知项置后，不根据 ID、文件时间或打开时刻补造。排序、分组和筛选仅作用于管理窗口，不改变主窗口下拉框的共享顺序、模板 ID 或保存内容。只有按保存顺序且不分组时开放拖放交换。流程与 POI 分别通过 TemplateSetting 记住视图模式、图标尺寸、排序方向和分组设置，随应用配置保存。

“查看 → 预览”独立控制可调宽度的右侧预览栏，图标和列表均可使用；每次打开管理窗口时默认关闭，展开后才读取、刷新当前项信息，折叠时取消加载并清空内容。选中流程后显示已保存结构、存储位置、执行次数、预计耗时、节点数、连接数及已有的修订版本，不打开编辑器或执行流程。切换选择、隐藏预览或关闭窗口会使旧加载结果失效，损坏预览不阻止编辑。执行次数统计 `FlowNodeRecordConfig` 指向的本机诊断库中保留的匹配运行记录，包含失败与未结束记录，不代表跨机器或全部历史总次数。身份沿用 `FlowIdentity` 的稳定 FlowKey 与旧记录兼容规则；预计耗时取当前已保存内容 hash 对应的最近至多 20 次正常完成记录平均耗时，排除失败、取消和仍在运行的样本，无可用样本显示“暂无数据”。无内容 hash 的旧模板使用同身份历史；有 hash 但历史缺少该列时不估算。`FlowTemplateUsageReader` 只读汇总字段，不初始化数据库、不启动写入队列、不读取节点 Payload；未展开预览时不查询统计。`TemplateBrowserPreviewTests` 与 `FlowTemplateUsageReaderTests` 验证折叠零刷新、异步失效、身份隔离和样本口径。

流程新建窗口的“创建来源”只提供“内置流程”和“现有模板”两个分区，页签显示当前搜索匹配数量；不列出默认模板或文件来源卡片。打开时选中首个可用内置流程并显示预览；现有模板分区按源索引准备独立副本。切换分区和搜索保留手动输入的名称；空分区或搜索无匹配时清空旧预览并禁用创建，Enter 也不能使用隐藏的旧来源创建。管理窗口原有导入入口及已准备的复制/导入来源仍可使用。内置库不可用时显示错误，只允许使用现有模板，不回退到默认模板。

新建窗口从随包只读 `Assets/Templates/BasicTemplates.db` 读取 9 项业务流程：相机取图、拍照与 POI、亮度均匀性、色度均匀性、棋盘格对比度、拍照条纹 MTF、图片条纹 MTF、图片发光区定位和通用传感器控制。`FlowTemplatePresets` 将版本化 `flow-recipe` 中的步骤映射为固定支持的节点，生成有连线的 STN 草稿，不接受任意类型名或任意节点属性。业务步骤参考反馈中的常见流程，采用当前节点实现；不复制现场设备代码、服务地址、校正名、参数模板名或图片路径。

右侧直接生成结构缩略图并显示运行前配置清单，不打开运行画布、不执行流程、不写封面缓存或用户模板库，也不自动创建关联参数模板。点击创建才按当前本地/MySQL 存储规则保存新流程，重复创建拥有独立身份；取消只丢弃草稿。创建后双击流程打开编辑器，按清单选择本机设备、校正与 POI/分析参数。

基础流程统一使用 `MQTTStartNode`（Start），服务器和端口取应用统一 MQTT 默认值，与右键新建 Start 一致。仅包含本地执行节点的流程可直接通过运行命令启动，无需 MQTT 就绪；其设备、结果存储或校正依赖仍由具体节点决定。均匀性与棋盘格流程中的服务端 `POIAnalysisNode` 通过开始节点发布请求，运行前须确保对应 MQTT 服务可用、配置算法设备并选择分析参数模板。所有预设都是待配置的业务起点，不能将结构预览或本地创建成功视为设备可运行。

拖拽以目标卡片标记提示交换位置，只交换源项和目标项，中间流程不动。提交成功后同步共享模板集合与主窗口下拉框，保留当前选中对象，管理窗口的保存顺序视图随之更新。MySQL 按现有序号约定，在事务内交换主记录 ID 和全部明细 Pid，流程名称、保存内容和资源标识跟随原模板；本地流程交换 SQLite sort_order，ID 不变。保存异步执行，失败在窗口内提示重新打开核对。再次打开按已记住的浏览设置展示；旧 flow_template_browser_order 本机偏好不参与排序，封面缓存仍独立保留。事务与身份边界见[模板持久化与排序](../../03-architecture/components/templates/design.md)。搜索后的编辑、复制、删除和导出按源对象定位；删除确认显示实际勾选范围，包括被搜索隐藏的勾选。`FlowTemplateBrowserOrderTests` 覆盖正负 ID 下的倒序浏览、筛选和源对象定位，以及数据库交换事务边界。

平铺封面直接采用已保存 STN 的节点坐标、尺寸和连接端口，不自动布局，也不改写流程。`FlowTemplateCoverRenderer` 从节点默认显示元信息读取字体、标题色、端口形状和控件位置，结合保存的标题、端口名称和普通控件显示值绘制圆角节点；连线复用编辑器的贝塞尔路由，按实际输入/输出端口位置连接，保留回连路径，不显示运行状态或全部节点参数。控件值按普通属性的本地化标签只读匹配，无法匹配的值显示“—”，不将构造时默认值当作保存值。仅请求可见区域的封面，后台串行生成固定 640×360 PNG，以包含布局的完整内容、显示配置和渲染版本校验缓存；保存后重新激活管理窗口或再次显示该项时按新内容取图。`FlowTemplateCoverService` 使用 `ColorVision.Local.db` 中独立的 `flow_template_covers` 表保存 PNG BLOB，最多保留最近生成的 1000 份可重建封面；同时保留最近使用的 64 份已解码、冻结的图片，重新打开窗口时直接复用，避免反复排队读取、解码和闪现占位提示。进程重启或内存缓存未命中时显示“正在加载预览…”，先读磁盘缓存，缺失或损坏才重新生成。封面跟随 `FlowEditorSettings` 的实际画布主题、网格、圆角和连线弯曲度；明暗主题及自定义配色、界面语言分别缓存，切换主题或显示设置会刷新卡片和已展开的预览，未展开的信息栏不查询或刷新详情。相同内容与显示配置可共享封面，重命名不需重新生成；缓存读写失败仍尝试显示内存预览，损坏流程或缺失节点类型显示“预览不可用”，不阻止打开编辑器。生成使用中性 STN 解码与 `STNode.CapturePreviewAppearance` 显示快照：可能为端口结构和显示元信息创建默认节点，但不恢复节点属性、不调用属性 setter、不连接运行节点、不调用 `OnEditorLoadCompleted`。新建流程预览复用同一渲染器，直接生成图片，不写封面缓存。封面缓存不改变流程的 MySQL/本地归属，也不代表服务器流程或依赖参数已经离线可用。

本地保存沿用窗口内容 hash 基线检查，并使用条件更新防止读取后发生的并发覆盖；删除后的旧窗口不能重新创建同号记录。版本历史和搜索继续使用独立的 `FlowCatalog.db`，其失败不改变配置保存结果。下表的主表、明细和资源关系仅适用于 MySQL 存储。

| 数据 | 当前职责 |
| --- | --- |
| `TemplateFlow.Code` | `flow` |
| `ModMasterModel` | 流程主表，`Pid == 11` |
| `ModDetailModel.ValueA` | 保存资源 ID，不是节点图本体 |
| `SysResourceModel` | `Type = 101`，`Value` 保存 Base64 STN |
| `FlowParam.DataBase64` | 宿主读取与保存的流程内容 |
| `FlowKey` / 内容 hash | 稳定流程身份与加载基线，不以可重排模板 ID 代替身份 |
| 本地 `.stn` | 独立文档的画布文件，不等于完整模板迁移包 |

数据库恢复和数据库工具的“更新流程节点”按钮可将旧节点标识写回当前类型，不实例化节点或重写参数；处理范围及失败边界见[流程节点标识更新](./mysql-recovery.md#流程节点标识更新)。这会改变数据库内容hash，已打开流程需要重新加载模板，继续保存时仍受原有并发检查保护。

## 保存与并发边界

`ViewFlow.TrySave` 校验画布并取得 STN 后，调用 `TemplateFlow.Save2DB`。MySQL 保存更新主表、明细和资源，失败回滚事务并抛出异常；本地保存更新完整配置文档。调用方不能把错误吞掉后标记已保存。窗口保存传入自己的 `FlowTemplateSaveCondition`，按加载时内容 hash 判断并发冲突，不能借另一个窗口已更新的共享对象基线覆盖较新的内容。

锁定已有资源行时使用 `FOR UPDATE`，加载基线不符时抛 `FlowTemplateConcurrencyException`；这些是 Flow 的专用保存规则，不扩展为普通 `ITemplate<T>` 的事务保证。`FlowParam` 的 `ResourceId`、`ResourceCode`、`FlowKey`、revision 和内容 hash 标为 `JsonIgnore`，属于运行时身份/基线，不是普通参数 JSON 序列化能完整迁移的字段。

`Save2DB` 成功后更新运行身份和加载 hash，再尝试记录本地 catalog revision / 搜索投影。`TryRecordCatalogRevision` 的失败只记录 `WARN` 并清空本次侧车 revision 信息，明确提示流程已保存、仅版本历史/节点搜索索引未更新，不影响流程保存和执行。保存成功与“版本/搜索索引可用”是两个检查点；该警告不表示主存储保存失败，也不表示索引功能已恢复。

本机版本历史恢复目标版本的完整 STN 快照，并追加新版本；重复框成员、重复次数和逐轮参数随快照恢复。用户确认替换未保存修改后，主流程与独立编辑窗口直接加载恢复内容，不再经过普通刷新的保存提示，避免旧画布再次保存覆盖恢复结果。历史窗口等待画布加载完成，加载失败会提示版本已保存但编辑器刷新失败；若用户已切换到另一份文档，不替换那份画布。`FlowVersionRestoreServiceTests`、`FlowTemplateWorkspaceEditingTests` 与 `ViewFlowDocumentBehaviorTests` 覆盖快照往返及两种编辑窗口的替换边界，不代表现场硬件已验收。

`FlowCanvasCatalogBuilder` 从 STND v1 构建语义、布局和搜索投影，不修改源画布，也不建立 live editor graph；codec 可短暂实例化节点发现默认 option schema。`STNodeInHub`、`STNodeOutHub`、`STNodeHub` 及其派生节点按各自保存的 `count` 扩展输入、输出或成对端口，保留空闲端口及原有端口顺序；类型缓存只保存默认结构，不把一个实例的端口数量套给另一个实例。索引恢复不调用 `OnLoadNode`、属性 setter 或真实连线事件，避免触发设备/MQTT行为。缺失、非四字节或非正数的动态数量会拒绝；整张画布最多分配 1,000,000 个端口，分配前检查预算。

多路接线引起端口增长是正常行为，不需要删除重建节点。更新后的索引器能直接解析已有流程；重新保存可生成本次版本和节点搜索索引。catalog revision 是不可变记录。旧 Artifact 表不再由当前保存/运行链读写或迁移，既有表与数据按兼容保留，不要求手工清库。

## 导出与删除的目标选择

`TemplateFlow.Export(index)` 和 `Delete(index)` 都先统计共享 `TemplateFlow.Params` 中的 `IsSelected`，再决定目标；列表中高亮的当前行与勾选项不是同一状态：

| 勾选数量 | 最终范围 |
| --- | --- |
| 0 | 使用调用方传入的索引 |
| 1 | 使用唯一勾选项，覆盖传入索引 |
| 多个 | 对所有勾选项执行多选导出或逐项删除 |

`Load()` 复用既有 `TemplateModel`，不清除其勾选状态；关闭模板管理窗口时的重新加载也不清除勾选。主工作区 `ViewFlow` 虽按 active 模板传入索引，导出/删除前却未清除共享勾选。因此当前画布、列表高亮和删除确认框中的流程名都不能单独保证最终操作范围。操作前检查实际勾选，只保留目标模板；删除前另行保留需要恢复的流程包。

MySQL 删除直接修改主表、明细和对应资源，没有 `Save2DB` 的事务封装；本地删除按配置文档逐条执行。不能把多项删除理解为全成或全败。导出读取模板保存的 `DataBase64`，不会自动保存当前编辑器画布。

## 单流程包与多选导出的区别

| 导出对象 | 内容与限制 |
| --- | --- |
| 单流程 `.cvflow` | `flow.stn`、`manifest.json` 及关联模板载荷 |
| 多选流程 | zip 内多个 `.stn`，不自动带 `.cvflow` 的关联模板 manifest |
| 独立窗口保存 | 由文档模式决定文件或数据库目标，不因按钮名为“导出”就等于 `.cvflow` |

本地存储模式下，`TemplateFlow.ImportFile` 校验 `.stn` 后可创建本地流程模板；MySQL 模式仍应由独立 `ViewFlow` 打开 STN，或通过 `.cvflow` 导入模板。需要迁移算法参数时不能只复制 STN，须在支持的服务模式中使用并核对关联模板包。

## `.cvflow` v3 契约

| 包内文件 | 作用 |
| --- | --- |
| `flow.stn` | 原样保存的 STND v1 画布二进制；包格式升级不改变画布格式 |
| `manifest.json` | 包版本、流程 SHA-256 与关联模板元数据 |
| `templates/<sha256>.json` | 按内容寻址的模板载荷；相同载荷在包内只保存一次 |

导出调用 `CollectTemplatesForExport` 扫描节点模板引用属性，如 `TempName`、`POITempName`、`SavePOITempName`、`OutputTemplateName`、`ModelName`，并继续扫描模板内容里的二级引用。

相机校正由目标相机管理，校正数据可能很大。`CalibTempName`、`CaliTempName` 只在流程及参数内容中保留名称，不作为流程包的关联模板收集，也不随同名算法模板的冲突映射改名。导入后，用户应重新选择目标相机的校正模板；包内保留原名称不保证目标相机具有对应校正。

导入先完整校验包：限制条目数、模板数、单项和总解压大小，校验流程与模板载荷 SHA-256，并验证 STND v1 内容。未知未来大版本明确拒绝；v1/v2 manifest 内联模板仍可兼容导入。哈希一致不是唯一合法性条件，损坏或不支持的 STN 仍应拒绝。

通过校验后，当前环境未注册的模板类型，以及缺少序列化内容且不能从 Mod 数据重建的关联模板，只保留原模板名称，不强制创建、不生成冲突副本，也不阻止流程导入。本地已有同名模板时继续沿用；本地不存在时仍保留引用，供运行时生成或之后配置。导入成功不代表这些引用在运行时一定可用。

其余可重建的模板按“模板类型 + 规范化有效内容”匹配本地模板：

1. 同名同内容直接复用；异名同内容映射到已有模板。
2. 同名不同内容创建带流程名的冲突副本；重复导入同包复用已创建的等价副本。
3. 名称映射同时更新关联模板的二级引用与 STN 节点引用，再将最终 STN 转成新流程模板的 Base64 内容。

导入和冲突处理不是“所有外部数据都可回滚”的承诺；应记录包来源、目标环境、名称映射和失败阶段，保留导入前可恢复的模板数据。

## 故障定位与验收

| 现象 | 第一检查点 |
| --- | --- |
| 流程能打开但保存失败 | 画布验证、当前 active 文档、加载 hash、`Save2DB` 异常及数据库事务 |
| 保存后重开没变化 | 保存目标是本地文件还是数据库；`ValueA` 是否指向实际更新资源 |
| 保存成功但历史/搜索缺项 | catalog 日志、`FlowKey`、投影构建；不因侧车失败重写生产流程 |
| 导出/删除对象与当前画布不同 | `TemplateFlow.Params` 的勾选项会优先于传入索引；核对列表勾选，而不只看高亮行 |
| 导入后模板名找不到 | manifest、模板内容匹配、冲突副本和二级引用替换 |
| 图正常但属性缺选择器 | [工作区](../../01-user-guide/workflow/design.md)及[PropertyGrid 契约](../ui-components/property-grid.md)，不是包格式问题 |
| 引擎结束但业务结果未完成 | [执行会话](../../01-user-guide/workflow/execution.md)的最终化判据，不在模板保存层补等待 |

`FlowPackageCompatibilityTests` 覆盖包完整性、旧版本、未来版本拒绝、模板去重和引用替换；`FlowTemplateIdentityTests` 覆盖身份及窗口保存条件；这些局部测试不等于真实 MySQL 事务、全部旧流程语料或现场导入已通过。

`Test/ColorVision.UI.Tests/LocalFlowTemplateStorageTests.cs` 使用临时 SQLite 验证本地模板操作、STN 画布编辑后的节点/连线恢复、身份与 POI 隔离、并发保存、删除后旧窗口保存及损坏内容拒绝；MySQL 读取失败用抛错的 client factory 模拟，不代表真实服务器或设备执行验收。

它不操作用户的本地库或真实相机。

`Test/ColorVision.UI.Tests/FlowTemplatePresetTests.cs` 验证全部基础流程的节点与连线往返、设备/路径引用留空、预览不落库、取消和独立创建身份，以及 Start 到结束的本地执行生命周期；不代表真实 MQTT、MySQL 或相机/算法服务已经验收。

授权验证至少核对：新增节点/参数保存后重开、并发窗口保存冲突、单流程包重导入不重复创建模板、冲突模板及二级引用正确、多选 zip 不被误认为完整迁移包。结果模型的历史 handler / 中立 overlay / 项目输出分流由[结果契约](./result-handoff-chain.md)维护。
