---
knowledge_id: "flow.templates"
knowledge_type: "topic"
status: "current"
summary: "Flow 模板的保存基线、导出/删除勾选范围、cvflow v3 包兼容，以及版本/搜索侧车的失败边界。"
aliases: ["Flow模板保存后参数为什么丢失","TemplateFlow","FlowPackageHelper","cvflow","FlowKey","FlowTemplateSaveCondition","FlowTemplateConcurrencyException","导入流程","关联模板","流程删除范围","流程多选导出","模板勾选项"]
code_paths: ["Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","Engine/ColorVision.Engine/Templates/Flow/FlowParam.cs","Engine/ColorVision.Engine/Templates/Flow/FlowTemplateSaveCondition.cs","Engine/ColorVision.Engine/Templates/Flow/FlowPackageHelper.cs","Engine/ColorVision.Engine/Templates/Flow/Versioning","Engine/ColorVision.Engine/FlowProcessing/Compilation/FlowCanvasCatalogBuilder.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs","Test/ColorVision.UI.Tests/FlowTemplateIdentityTests.cs","Test/ColorVision.UI.Tests/FlowCanvasCatalogBuilderTests.cs","Test/ColorVision.UI.Tests/FlowCatalogServiceTests.cs"]
related: ["flow.architecture","flow.workspace","flow.session","flow.headless","engine.template-design","engine.results"]
---

# Flow 模板、持久化与流程包

`TemplateFlow` 管理数据库中的流程定义；`FlowPackageHelper` 负责 `.cvflow` 及其关联模板。它们不拥有画布交互、节点运行或客户最终判定。编辑对象与文件/数据库保存目标见[工作区契约](../../01-user-guide/workflow/design.md)，执行与后处理见[执行会话](../../01-user-guide/workflow/execution.md)。

导入会创建或复用关联模板，保存/删除会修改数据库。查询格式或验证文档不授权实际导入、删除或覆盖生产模板；导出可能包含客户参数，分享前须确认范围并脱敏。

## 模板发现与主存储

模板初始化与注册由[模板核心契约](../../03-architecture/components/templates/design.md)维护。列表为空先检查数据库与程序集发现，不先改菜单；以下仅描述 Flow 自己的存储、身份与包行为。

| 数据 | 当前职责 |
| --- | --- |
| `TemplateFlow.Code` | `flow` |
| `ModMasterModel` | 流程主表，`Pid == 11` |
| `ModDetailModel.ValueA` | 保存资源 ID，不是节点图本体 |
| `SysResourceModel` | `Type = 101`，`Value` 保存 Base64 STN |
| `FlowParam.DataBase64` | 宿主读取与保存的流程内容 |
| `FlowKey` / 内容 hash | 稳定流程身份与加载基线，不以可重排模板 ID 代替身份 |
| 本地 `.stn` | 独立文档的画布文件，不等于完整模板迁移包 |

## 保存与并发边界

`ViewFlow.TrySave` 校验画布并取得 STN 后，调用 `TemplateFlow.Save2DB`。数据库保存更新主表、明细和资源，失败回滚事务并抛出异常；调用方不能把错误吞掉后标记已保存。窗口保存传入自己的 `FlowTemplateSaveCondition`，按加载时内容 hash 判断并发冲突，不能借另一个窗口已更新的共享对象基线覆盖较新的内容。

锁定已有资源行时使用 `FOR UPDATE`，加载基线不符时抛 `FlowTemplateConcurrencyException`；这些是 Flow 的专用保存规则，不扩展为普通 `ITemplate<T>` 的事务保证。`FlowParam` 的 `ResourceId`、`ResourceCode`、`FlowKey`、revision 和内容 hash 标为 `JsonIgnore`，属于运行时身份/基线，不是普通参数 JSON 序列化能完整迁移的字段。

`Save2DB` 成功后更新运行身份和加载 hash，再尝试记录本地 catalog revision / 搜索投影。`TryRecordCatalogRevision` 的失败只记录日志并清空本次侧车 revision 信息，不能把已经成功的 MySQL/STN 保存伪报为失败。保存成功与“版本/搜索索引可用”是两个检查点。

`FlowCanvasCatalogBuilder` 从 STND v1 构建语义、布局和搜索投影，不修改源画布，也不建立 live editor graph；codec 可短暂实例化节点发现 option schema。catalog revision 是不可变记录。旧 Artifact 表不再由当前保存/运行链读写或迁移，既有表与数据按兼容保留，不要求手工清库。

## 导出与删除的目标选择

`TemplateFlow.Export(index)` 和 `Delete(index)` 都先统计共享 `TemplateFlow.Params` 中的 `IsSelected`，再决定目标；列表中高亮的当前行与勾选项不是同一状态：

| 勾选数量 | 最终范围 |
| --- | --- |
| 0 | 使用调用方传入的索引 |
| 1 | 使用唯一勾选项，覆盖传入索引 |
| 多个 | 对所有勾选项执行多选导出或逐项删除 |

`Load()` 复用既有 `TemplateModel`，不清除其勾选状态；关闭模板管理窗口时的重新加载也不清除勾选。主工作区 `ViewFlow` 虽按 active 模板传入索引，导出/删除前却未清除共享勾选。因此当前画布、列表高亮和删除确认框中的流程名都不能单独保证最终操作范围。操作前检查实际勾选，只保留目标模板；删除前另行保留需要恢复的流程包。

删除直接修改主表、明细和对应资源，没有 `Save2DB` 的事务封装；不能把多项删除理解为全成或全败。导出读取模板保存的 `DataBase64`，不会自动保存当前编辑器画布。

## 单流程包与多选导出的区别

| 导出对象 | 内容与限制 |
| --- | --- |
| 单流程 `.cvflow` | `flow.stn`、`manifest.json` 及关联模板载荷 |
| 多选流程 | zip 内多个 `.stn`，不自动带 `.cvflow` 的关联模板 manifest |
| 独立窗口保存 | 由文档模式决定文件或数据库目标，不因按钮名为“导出”就等于 `.cvflow` |

`TemplateFlow.ImportFile` 不是本地 `.stn` 的可靠模板导入路径；本地画布应由独立 `ViewFlow` 打开。需要迁移算法参数时不能只复制 STN，须使用并核对关联模板包。

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

`FlowPackageCompatibilityTests` 覆盖包完整性、旧版本、未来版本拒绝、模板去重和引用替换；`FlowTemplateIdentityTests` 覆盖身份及窗口保存条件；`FlowCanvasCatalogBuilderTests` / `FlowCatalogServiceTests` 覆盖投影与版本目录。这些局部测试不等于真实 MySQL 事务、全部旧流程语料或现场导入已通过。

授权验证至少核对：新增节点/参数保存后重开、并发窗口保存冲突、单流程包重导入不重复创建模板、冲突模板及二级引用正确、多选 zip 不被误认为完整迁移包。结果模型的历史 handler / 中立 overlay / 项目输出分流由[结果契约](./result-handoff-chain.md)维护。
