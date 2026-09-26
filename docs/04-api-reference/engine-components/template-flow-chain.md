---
knowledge_id: "flow.templates"
knowledge_type: "topic"
status: "current"
summary: "Flow 本地 SQLite 与 MySQL 配置存储、保存基线、导出/删除勾选范围、cvflow v3 包兼容，以及版本/搜索侧车的失败边界。"
aliases: ["Flow模板保存后参数为什么丢失","TemplateFlow","FlowPackageHelper","cvflow","FlowKey","FlowTemplateSaveCondition","FlowTemplateConcurrencyException","导入流程","关联模板","流程删除范围","流程多选导出","模板勾选项","StnV1NeutralCodec","动态端口索引","逻辑与索引警告","本地流程","离线流程模板","LocalFlowTemplateStorage","流程封面","流程平铺","FlowTemplateManagerWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","Engine/ColorVision.Engine/Templates/Flow/LocalFlowTemplateStorage.cs","Engine/ColorVision.Engine/Templates/Flow/FlowParam.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateSaveCondition.cs","Engine/ColorVision.Engine/Templates/Flow/FlowPackageHelper.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateManagerWindow.cs","Engine/ColorVision.Engine/Templates/Browser","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateCover.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateCoverService.cs","Engine/ColorVision.Engine/Templates/Flow/Versioning","Engine/ColorVision.Engine/FlowProcessing/Compilation/FlowCanvasCatalogBuilder.cs","Engine/ColorVision.Engine/FlowProcessing/Compilation/StnV1NeutralCodec.cs"]
test_paths: ["Test/ColorVision.UI.Tests/StartupFlowTemplateLoadingTests.cs","Test/ColorVision.UI.Tests/LocalFlowTemplateStorageTests.cs","Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs","Test/ColorVision.UI.Tests/FlowTemplateIdentityTests.cs","Test/ColorVision.UI.Tests/FlowCanvasCatalogBuilderTests.cs","Test/ColorVision.UI.Tests/FlowCatalogServiceTests.cs","Test/ColorVision.UI.Tests/FlowTemplateCoverTests.cs","Test/ColorVision.UI.Tests/FlowTemplateBrowserOrderTests.cs","Test/ColorVision.UI.Tests/OfflineConfigurationEntryTests.cs"]
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

工作流程齿轮（包括共用命令的画布入口）、“模板 → 流程”菜单、ProjectARVRPro、ProjectKB、ProjectLUX 和 Conoscope 的流程模板管理使用专用 `FlowTemplateManagerWindow`；窗口与 POI 图标浏览器共用 `TemplateBrowserWindow` 的浏览交互，封面仍由流程专用组件提供。旧 `TemplateEditorWindow` 保留，可从新窗口右上角“更多 → 旧版管理”打开。新窗口默认显示上方封面、下方居中的单行名称的平铺，也可切回列表；长名称悬停查看，双击仍打开 `FlowEngineToolWindow`。切换复用同一窗口内的集合视图，保留顺序、筛选、高亮、勾选和两种模式各自的滚动位置，不重新加载模板、不筛选主窗口下拉框。切换视图不改变窗口尺寸。搜索与新建、导入/导出位于顶部；底部显示选择数量、删除、保存名称修改和关闭操作。拖拽以目标卡片标记提示交换位置，只交换源项和目标项，不逐项移动中间流程，不调用通用模板的数据库换号排序，不修改运行集合或主窗口下拉框。顺序异步保存到本机 `ColorVision.Local.db` 的 `flow_template_browser_order` 表，服务器来源按主机、端口、数据库区分，本地来源单独保存；它是浏览偏好，不同步服务端。保存失败在窗口内提示并可用“保存”重试；重新打开时按稳定流程标识恢复顺序，新流程追加，已删除项忽略。搜索后的编辑、复制、删除和导出按源对象定位；删除确认显示实际勾选范围，包括被搜索隐藏的勾选。

平铺封面由已保存 STN 的节点位置、标题和连线生成结构缩略图，不显示运行状态或全部节点参数。仅请求可见区域的封面，后台串行生成固定 640×360 PNG，以包含布局的完整内容 hash 和渲染版本校验缓存；保存后重新激活管理窗口或再次显示该项时按新内容取图。`FlowTemplateCoverService` 使用 `ColorVision.Local.db` 中独立的 `flow_template_covers` 表保存 PNG BLOB，最多保留最近生成的 1000 份可重建封面。同一内容可共享封面，重命名不需重新生成；缓存读写失败仍尝试显示内存预览，损坏流程或缺失节点类型显示“预览不可用”，不阻止打开编辑器。生成复用目录投影：可能为端口结构创建默认节点，但不恢复节点属性、不连接运行节点、不调用 `OnEditorLoadCompleted`。封面缓存不改变流程的 MySQL/本地归属，也不代表服务器流程或依赖参数已经离线可用。`FlowTemplateCoverTests` 与 `OfflineConfigurationEntryTests` 分别验证缓存/生成边界和窗口模式/入口，不替代现场模板与插件的兼容性验收。

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

`FlowPackageCompatibilityTests` 覆盖包完整性、旧版本、未来版本拒绝、模板去重和引用替换；`FlowTemplateIdentityTests` 覆盖身份及窗口保存条件；`FlowCanvasCatalogBuilderTests` / `FlowCatalogServiceTests` 覆盖投影与版本目录，包括新建逻辑与节点多路接线、同类型不同端口数量、汇聚/分发节点往返、索引不执行加载/连线回调及非法数量拒绝。这些局部测试不等于真实 MySQL 事务、全部旧流程语料或现场导入已通过。

`Test/ColorVision.UI.Tests/LocalFlowTemplateStorageTests.cs` 使用临时 SQLite 验证本地模板操作、STN 画布编辑后的节点/连线恢复、身份与 POI 隔离、并发保存、删除后旧窗口保存及损坏内容拒绝；MySQL 读取失败用抛错的 client factory 模拟，不代表真实服务器或设备执行验收。

`Test/ColorVision.UI.Tests/OfflineConfigurationEntryTests.cs` 使用临时配置和 WPF 窗口验证空列表管理入口、工具栏命令绑定、首个流程创建与选中、未绑定画布首次保存和取消。它不操作用户的本地库或真实相机。

授权验证至少核对：新增节点/参数保存后重开、并发窗口保存冲突、单流程包重导入不重复创建模板、冲突模板及二级引用正确、多选 zip 不被误认为完整迁移包。结果模型的历史 handler / 中立 overlay / 项目输出分流由[结果契约](./result-handoff-chain.md)维护。
