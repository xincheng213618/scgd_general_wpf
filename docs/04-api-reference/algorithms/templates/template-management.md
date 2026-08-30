---
knowledge_id: "algorithms.template-management"
knowledge_type: "topic"
status: "current"
summary: "TemplateEditorWindow与TemplateCreateView的共享参数、创建来源、预览、索引和关闭语义；关闭不是通用回滚，筛选后的操作目标需单独核对。"
aliases: ["模板类存在但列表不显示","模板编辑","模板创建","取消模板编辑","模板搜索后删除","TemplateEditorWindow","TemplateCreate","TemplateCreateView","TemplateCreateSourceKind","ApplyTemplateSource","IsUserControl","IsSideHide","TemplateSearchProvider","TemplateSettingEdit","TemplatesExtension"]
code_paths: ["Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs","Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml","Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs","Engine/ColorVision.Engine/Templates/TemplateCreateView.xaml.cs","Engine/ColorVision.Engine/Templates/ITemplate.cs","Engine/ColorVision.Engine/Templates/TemplateModel.cs","Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs","Engine/ColorVision.Engine/Templates/TemplateSettingEdit.xaml.cs","Engine/ColorVision.Engine/Templates/TemplatesExtension.cs"]
test_paths: []
related: ["engine.template-design","algorithms.template-menus","algorithms.json-templates","flow.workspace","ui.property-grid","ui.configuration"]
---

# 模板编辑与创建宿主

`TemplateEditorWindow` 是已有模板列表和编辑宿主；`TemplateCreateView` 负责准备创建来源与预览，`TemplateCreate` 只包装独立创建对话框。它们调用具体 `ITemplate`，不提供统一的数据库事务、撤销系统或隔离编辑会话。

注册身份、参数加载和普通持久化的唯一说明在[模板注册、参数与持久化](../../../03-architecture/components/templates/design.md)。[模板菜单](./template-menu-entries.md)、[JSON 编辑器](./json-templates.md)和[Flow 工作区](../../../01-user-guide/workflow/design.md)各有独立入口与实现，不因共用模板窗口就拥有相同保存和运行语义。

## 打开、选择、保存和关闭

| 阶段 | 当前调用链 | 不能据此推断 |
| --- | --- | --- |
| 构造编辑窗口 | 先同步 `template.Load()`，再初始化控件；绑定 `ItemsSource` 与初始索引 | 打开窗口是只读操作、数据库与全部条目都健康 |
| 普通条目选择 | 按选中对象找源集合索引，调用 `SetSaveIndex`，把 `GetParamValue(index)` 直接交给 PropertyGrid | 已建立独立工作副本，或只有真正修改的条目才被保存 |
| 自定义条目选择 | `SetSaveIndex` 后调用 `SetUserControlDataContext(index)` | 自定义控件一定复制参数或支持撤销 |
| 保存按钮 | `ITemplate.Save()`，随后关闭窗口 | 多条 SQL 都已提交、所有消费者都已刷新 |
| 保存快捷命令 | `ITemplate.Save()` 正常返回后显示成功提示，窗口不关闭 | 提示验证了影响行数或数据库事务 |
| 关闭 | 再调用 `ITemplate.Load()` | 回滚已写数据库、撤销创建/删除/排序，或离线时恢复未保存值 |

普通属性面板收到的是共享参数对象，修改可立即被同一对象的其它持有者看见。重命名的 `TemplateModel<T>.Key` 也直接改 `Value.Name`；Enter、失焦或窗口停用只是退出名称编辑状态，不是名称校验、保存或回滚。普通保存索引是位置，不是稳定记录 ID。

关闭时重新加载的具体效果取决于子类。普通泛型加载会替换既有包装项的 `Value`，但不移除数据库已不存在的条目；离线时不会恢复旧值。并行窗口、静态 `Params` 和旧参数引用不具备会话隔离，不能把关窗当作可靠的“放弃更改”。

## 编辑控件与创建预览是不同钩子

`IsUserControl=false` 时使用 PropertyGrid；为 `true` 时，编辑宿主调用 `GetUserControl()`，必要时从原 Grid 移除该控件并重新挂载。若模板实例缓存同一控件，打开另一个窗口不保证得到新的控件实例或独立状态。

`IsSideHide=true` 会隐藏右侧编辑区域并改变布局；列表双击仍交给 `PreviewMouseDoubleClick(sourceIndex)`，由具体模板决定打开什么。Flow/POI 的独立编辑行为不能从普通 PropertyGrid 路径推定。

创建预览另用 `CreateDefault()`、`CreateUserControl()` 和可选的 `ITemplateUserControl.SetParam()`，不是复用 `GetUserControl()` / `SetUserControlDataContext()`。只实现已有模板编辑控件，并不保证新建预览也正确；基类 `CreateUserControl()` 仅返回空控件。

## 创建来源、预览与取消

`TemplateCreateView.Initialize` 构建来源列表；是否展示现有副本、文件导入，取决于反射判断 `CopyTo(int)` / `Import()` 是否被覆写，不是运行能力测试。默认来源始终列出，已有暂存内容时增加 Prepared；请求 Existing 时先找指定索引，找不到再找同类首项，请求类别不存在才回退 Prepared、Default。

| 来源 | `ApplyTemplateSource` 的动作 | 重要边界 |
| --- | --- | --- |
| `Default` | 清理模板暂存，随后准备默认预览 | 默认值可能读取字典/数据库，不是纯本地空对象 |
| `Prepared` | 使用当前 `HasCreateTemplateSource` 所表示的已准备内容 | 不重新读取原文件，不证明所有资源引用有效 |
| `Existing` | 对该源集合索引调用 `CopyTo` | 副本准备的深度、ID重映射和失败状态由子类决定 |
| `File` | 调用 `Import()`，成功后使用其暂存内容和 `ImportName` | 文件选择完成不等于模板已创建；导入本身可能有子类副作用 |

来源应用失败时会恢复之前的列表选择或显示错误，但没有对具体模板内部状态做事务回滚。切换来源会重建预览；离开 Prepared 后会移除该来源条目。来源搜索只筛选标题、描述和来源标签，不重新查询数据库。

非 `IsSideHide` 模板会调用 `CreateDefault` 准备预览参数：普通控件直接编辑该对象，自定义控件通过创建预览钩子接收它。`IsSideHide` 跳过预览，所以不能假定每条创建路径都已准备 `CreateTemp`。预览异常会隐藏面板并显示错误，却没有一个统一的“预览失败则禁止创建”门禁；创建能否完整落库取决于具体实现，普通实现没有统一的完整性保证。

创建按钮 Trim 名称，检查非空、`ExitsTemplateName` 和已选来源，然后调用 `TryCreateTemplate`。成功时清暂存并发出 `TemplateCreated`；独立对话框记录 `CreateName`、设置 `DialogResult=true`。这只是所调用创建路径的报告，不是独立的数据库验收。

取消、Esc 或未成功创建就关闭对话框，会调用 `Discard`：清理模板创建来源、选中来源和预览控件。它不删除可能已经创建的数据库记录，不回滚子类导入副作用，也不恢复另一个窗口持有的共享对象。新建、复制和导入的调用方还应核对实际集合/数据库状态，不能只看对话框结果。

## 搜索后的显示位置不总是源位置

编辑窗口的搜索把 `ListView.ItemsSource` 换成过滤结果。选择、鼠标双击、创建副本与拖动使用对象引用查找源索引；但当前删除、导出、部分重命名和“设为默认”入口仍直接使用可见 `SelectedIndex`，没有全部经过该映射。

这是现有实现风险，不是推荐行为：筛选后的第零行未必是源集合第零项。再加上具体 `Delete/Export` 可能优先使用集合的 `IsSelected` 勾选项，不能宣称操作一定只作用于当前可见选中项。执行写入/删除前必须核对实际对象及记录 ID；文档验证不执行这些动作。

列头排序改变的是源集合顺序；拖动则逐步调用 `SwapTemplateOrder`，可能已经完成前面的交换后才失败。具体模板的排序可能写数据库甚至改变身份，不能把“整理列表”当作无副作用显示操作；普通基类边界见持久化主题。

`TemplatesExtension.CreateEmpty` 是下拉列表适配：新增 Empty 项，并复用源集合中的包装对象；监听 Add/Remove/Reset，不是完整的 Move/Replace 同步或独立深复制。使用这种列表的调用方要处理空项与索引偏移，不直接拿显示索引写入模板源集合。

## 全局搜索和模板设置

`TemplateSearchProvider.GetSearchItems()` 从当前注册模板取得名称，构造搜索项。执行时再按名称找到首个所属模板：`IsSideHide` 调用其双击入口，否则新建非模态 `TemplateEditorWindow`。这不是实时数据库搜索；注册、名称快照、全局搜索缓存和随后加载可能处于不同阶段。

`TemplateSetting` 由 `ConfigService` 解析，是共享窗口配置，例如列表列可见性；取得配置对象不等于已保存文件，它也不同于数据库模板参数。该配置的保存与重载见[软件配置契约](../../ui-components/configuration.md)。

`TemplateSettingEdit` 的数据库重置入口调用具体模板的 `GetMysqlCommand().GetRecover()`；确认后通过 `BatchSqlConsumer.ExecuteAfterCommit` 执行，提交后重载 `SymbolCache`。它不是普通模板列表刷新，可能改变字典/数据库内容；必须单独具备授权与可用恢复依据，不能为了排查“模板没出现”就执行。

## 验证入口与缺口

本主题没有登记覆盖普通编辑窗口和创建来源的直接自动化测试。属性编辑器、菜单发现、Flow 包和节点绑定测试只覆盖各自局部契约，不能证明共享对象取消、过滤后目标选择、控件复用或普通数据库 CRUD 正确。

有授权时，应以隔离数据库和非敏感样例分别验证：打开/关闭、真实目标身份、默认/副本/文件来源、预览失败、保存部分失败与旧引用。记录已执行的最小用例和缺口；不要通过重置现场数据库、启动真实流程或删除模板来验证文档。
