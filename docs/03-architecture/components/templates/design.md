---
knowledge_id: "engine.template-design"
knowledge_type: "topic"
status: "current"
summary: "TemplateControl注册与普通ITemplate<T>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。"
aliases: ["模板架构","Templates目录","模板注册","如何新增算法模板","新增模板要继承什么","ITemplate","IITemplateLoad","TemplateControl","TemplateDicId","TemplateModel","ParamModBase","ModelBase","SaveIndex","TryCreateTemplate","SwapTemplateOrder"]
code_paths: ["Engine/ColorVision.Engine/Templates/ITemplate.cs","Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/ColorVision.Engine/Templates/ModelBase.cs","Engine/ColorVision.Engine/Templates/ParamModBase.cs","Engine/ColorVision.Engine/Templates/TemplateModel.cs","Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs","Engine/ColorVision.Engine/Dao/ModMasterModel.cs","Engine/ColorVision.Engine/Dao/ModDetailModel.cs","Engine/ColorVision.Engine/Templates/ImageCropping/TemplateImageCropping.cs","Engine/ColorVision.Engine/Templates/ARVR/SFR/TemplateSFR.cs","UI/ColorVision.UI/AssemblyHandler.cs","UI/ColorVision.Common/MVVM/ViewModelBaseExtensions.cs","Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs"]
test_paths: ["Test/ColorVision.UI.Tests/AlgorithmNodeTemplateMappingTests.cs"]
related: ["engine.index","algorithms.template-management","algorithms.json-templates","flow.templates","ui.property-grid","engine.results"]
---

# 模板注册、参数与持久化

`Engine/ColorVision.Engine/Templates/` 同时包含参数适配、数据库访问和 WPF 宿主。这里统一维护模板注册及普通 `ITemplate<T>` 的默认实现，不把它描述为纯接口层，也不把某一子类的行为套给所有模板。

编辑窗口、创建来源与取消语义见[模板编辑与创建宿主](../../../04-api-reference/algorithms/templates/template-management.md)；[JSON 模板](../../../04-api-reference/algorithms/templates/json-templates.md)和[Flow 模板、持久化与流程包](../../../04-api-reference/engine-components/template-flow-chain.md)分别维护自己的存储与兼容规则。读取本页不授权连接现场数据库、创建/删除模板、运行算法或重置字典。

## 注册身份、模板名称与数据库身份

| 对象 | 实际含义 | 不应混用为 |
| --- | --- | --- |
| `ITemplate` | 具体基类，不是 C# interface；包含默认钩子和构造注册 | 所有子类都实现完整 CRUD 的保证 |
| `IITemplateLoad` | `TemplateControl` 查找并调用的加载接口 | 模板菜单或数据库记录 |
| `ITemplate.Name` / 注册表键 | 基类构造时按 `Name ?? Code ?? GetType().ToString()` 取值并注册 | 后续修改的 `Code` 或某条参数的名称 |
| `Code` / `Title` | 模板族的业务代码与显示标题，由子类设置 | 自动更新注册表键的操作 |
| `TemplateDicId` | 普通模板主记录 `Pid` 对应的字典身份 | 模板主记录自身 `Id`、设备资源 ID |
| `TemplateModel<T>.Key` / `Id` | 代理 `Value.Name` / `Value.Id`，表示一条参数记录 | 集合索引或不可变的跨系统身份 |

`AddITemplateInstance` 对同一键会替换已有实例，不报重复注册错误，也不会卸载旧对象。常见子类在基类构造之后才设置 `Code`、`Title` 和共享 `Params`；不要按最终显示代码推断字典键。静态参数集合是具体子类的选择，泛型基类本身默认创建实例集合。

`TemplateControl.ExitsTemplateName` 在已注册实例的当前名称集合中做忽略大小写检查；它不是数据库唯一约束或并发预约。`FindDuplicateTemplate` 返回第一个命中的模板族。普通 `GetTemplateIndex` 却按精确名称相等查找，不能假定所有名称 API 的大小写规则一致。`NewCreateFileName` 尝试追加数字，也不构成全局唯一身份保证。

## 发现和加载是两个阶段

`TemplateInitializer.Order = 4`，初始化时经 UI Dispatcher 获取 `TemplateControl`。控制器首次构造调用 `Init`，并订阅 MySQL 连接变化，在 Dispatcher 上再次调用 `Init`；未连接时直接返回。

连接可用后，`AssemblyHandler.LoadImplementations<IITemplateLoad>()` 从程序集/类型缓存发现可实例化类型，要求具体类和公开无参构造；每次调用创建实例，构造失败记日志并跳过。控制器逐个调用 `Load()`，单个加载异常记日志后继续其它加载器。因此“初始化完成”日志不代表每个模板都成功，实例构造已注册也不代表参数已加载。

晚加载程序集还涉及 `AssemblyHandler` 缓存刷新；模板控制器不提供独立的插件热卸载/全量重建协议。列表为空时按程序集发现、构造、注册键、数据库连接和具体 `Load` 分支定位，不先添加菜单。扩展发现的共同边界见[UI 发现链](../../../04-api-reference/ui-components/ui-runtime-handoff.md)。

## 选择参数与存储分支

| 分支 | 载荷与默认值来源 | 维护边界 |
| --- | --- | --- |
| 普通 `ITemplate<T>`，`T : ParamModBase, new()` | `ModMasterModel` 加 `ModDetailModel`；默认明细来自 `TemplateDicId` 对应的系统字典 | 本页后续描述此基类默认实现，子类覆写优先核对 |
| `ITemplateJson<T>`，`T : TemplateJsonParam, new()` | `ModMasterModel.JsonVal`；默认 JSON 来自字典主记录 | 直接继承 `ITemplate`，不是普通泛型模板的子类；不能套用明细表行为 |
| POI、设备模板等专用实现 | 可能使用独立表、设备资源关联或专用编辑窗口 | 即使继承普通泛型基类，也必须核对具体覆写；例如 `TemplatePoi` 使用 POI 表 |
| `TemplateFlow` | 流程主表、明细、资源中的 STN 及身份基线 | 事务、并发冲突、包与侧车统一归 Flow 主题 |

`ParamBase` 定义在 `ModelBase.cs`，提供 `Id`、`Name`；`ParamModBase` 持有主记录和明细集合。`ModelBase` 通过 `SymbolCache` 把明细的 `SysPid` 映射为字典 `Symbol`，属性通常按名称读写该映射。符号缺失的明细不会进入参数字典，重复符号只保留首次映射；`GetDetail` 取的是映射后的值，不保证导出全部原始明细。

属性赋值通常先更新内存明细 `ValueA`，将旧值放到 `ValueB`，并触发属性通知；这不是写数据库或历史版本事务。转换也不是任意类型 schema：已建立参数映射后，缺少字段可能返回类型默认值，部分数值解析会抛错。字段/字典重命名、文化格式和新增默认值必须用真实旧载荷核对。

## 普通 Load 不等于完整刷新或回滚

`ITemplate<T>.Load()` 先清 `SaveIndex`，再按现有包装项的 ID 建备份。连接可用时查询 `Pid == TemplateDicId`、`TenantId == 0`、`IsDelete == false` 的主记录及对应明细，用 `(ModMasterModel, List<ModDetailModel>)` 构造参数。

- 已有 ID 保留 `TemplateModel<T>` 包装对象，但替换其 `Value`；新增 ID 追加到集合。持有旧参数对象的消费者不会自动换引用。
- 不主动移除数据库中已不存在的旧集合项，也没有声明排序恢复。
- 未连接时仍已清 `SaveIndex`，但不重新读取或恢复对象。查询或构造中途异常也没有整集合回滚。

所以关闭编辑器所调用的 `Load` 不能作为“取消全部改动、刷新全部消费者、清除所有旧条目”的证明。

## 普通 Save、创建和删除的完成条件

`SetSaveIndex` 只把整数索引去重加入列表，不追踪字段差异，也不自动随集合重排绑定到同一 ID。普通 `Save()` 按仍在范围内的索引依次更新主记录名称及 `GetDetail` 得到的明细；无索引就直接返回。保存后此基类不清 `SaveIndex`，与某些子类覆写不同。

普通 `Save(TemplateModel<T>)` 同样先写主记录再写明细。这些默认保存路径没有显式事务、不检查每条命令的影响行数；抛错前可能已有写入，正常返回也不证明每条目标记录存在。不能把窗口提示成功、无异常或方法名 `Save` 当作跨表原子提交。

`CreateDefault()` 查询字典明细构造 `CreateTemp`，有 `ImportTemp` 时再经 `CopyFrom` 应用来源；预览不是无依赖的纯对象构造。`CopyFrom` 是反射复制，部分对象递归、其它成员可能共用引用，单个成员异常会记录后继续，不是任意对象图的无损深克隆。

普通 `Create(name)` 先插主记录，再写明细并添加内存项；与普通 `Save` 一样没有包住全链的事务。它依赖准备好的 `CreateTemp`：该分支会把明细 `Pid` 改为新主 ID；未准备预览的默认分支当前创建明细时使用 `Pid = -1`，不能将直接调用 `Create(name)` 等同于完整 UI 创建链。公开 `AddParamMode(name, resourceId)` 是另一个方法，会为默认明细设置新主 ID，并可绑定资源；不要混同两条创建路径。

`TryCreateTemplate` 捕获 `Create` 异常并返回消息，以集合数量增长、当前名称出现或全局名称存在判断成功。它不是数据库核验，更不是失败补偿；返回 `false` 也不能据此假定前面的数据库写入已撤销。无有效参数的分支还可能询问是否通过 `GetMysqlCommand().GetRecover()` 重置数据库项，不能将这个恢复动作当作普通验证步骤。

普通 `Delete(index)` 先检查集合中 `IsSelected` 勾选项：一个勾选项覆盖传入索引，多个勾选项逐个删除，否则用传入索引。它直接删除主表与明细，再移除内存项，不是软删除，没有跨条目事务或通用引用完整性检查。Flow 节点、项目和结果中残留的名称/ID引用须由各自调用链核对。

## 复制、导入、导出与排序不是统一迁移事务

- `CopyTo(index)` 将当前参数 JSON 序列化再反序列化到 `ImportTemp`，只显式把参数 `Id` 置为 `-1`；不会在此刻创建数据库记录，也不保证全部嵌套 ID、资源引用已重映射。
- 普通 `ImportFile` 读取文件、先构造默认参数，再按来源明细的 `SysPid` 将 `ValueA` 拷入目标明细。它不是任意 JSON 字段合并；来源字典项不匹配可能在 `First(...)` 处失败。只捕获 JSON 异常，文件 I/O、字典/构造和其它异常可传播，失败前的临时状态也不保证全恢复。
- 普通单项导出是 `.cfg` 参数 JSON，多选是多个 `.cfg` 的 zip；基类导入对话框只选择 `.cfg`，不能据多选导出推断它支持整包回导。导出不自动包含设备、图像、相关模板或历史结果，失败也没有目标文件原子替换保证。
- `SwapTemplateOrder` 默认实现试图用临时 ID 交换主记录身份、明细 `Pid` 和内存集合，不只是修改界面排序。它没有显式事务和影响行数核验，异常返回 `false`；不能据返回值证明数据库与外部引用完整一致。需要授权数据库验证，不把拖动顺序当作只读整理。

JSON、POI、Flow 可覆写上述方法。尤其 JSON 的“设为默认”与 Flow 保存具有各自事务规则，不能总结成“所有模板保存都一样”或“所有模板都没有事务”。

## 新模板与消费者的接入边界

新增普通模板可参考 `Templates/ImageCropping/TemplateImageCropping.cs` 的参数集合、字典 ID 和加载接口；自定义编辑控件可参考 `Templates/ARVR/SFR/TemplateSFR.cs`，但还需区分编辑控件和创建预览钩子。除无参构造外，默认加载/创建需要对应的参数构造签名。

参数属于设备时保持设备的资源关联；属于客户判定、报表或 MES 格式时放回项目包，不因为有模板窗口就移入通用层。算法适配器如何把模板名称/ID、POI 等写入 `CVTemplateParam`，应追实际 `Algorithm*` 请求实现，而不是由模板基类推定已经接入。

Flow 常规属性通过 `FlowNodePropertyEditorAttribute` / `PropertyEditorTypeAttribute` 和 Engine 注册桥接入；只有类型级、多模板或动态选择器才使用 `FlowProcessing/Editor/NodeConfiguration/`。选择、缓存与验证归[PropertyGrid 契约](../../../04-api-reference/ui-components/property-grid.md)。历史结果 DAO/`ViewHandle*`、中立算法 overlay 和项目结果分别遵守[结果展示边界](../../../04-api-reference/engine-components/result-handoff-chain.md)，不属于模板保存的完成条件。

## 验证入口与缺口

`AlgorithmNodeTemplateMappingTests` 当前仅验证 ARVR 的 `POITempName` 映射到指定编辑器，不证明普通模板 CRUD。Flow 身份/流程包测试由 Flow 主题维护，不能拿它们代表全部 `ITemplate<T>`、字典迁移或编辑器行为。

尚未登记覆盖上述普通模板注册冲突、离线/增量重载、跨表部分写入、默认创建、过滤后目标选择与排序身份变化的完整自动化测试。修改这些代码时，需用隔离数据库和已授权样例核对持久化结果、旧模板字段与引用，并明确未覆盖的 UI/设备路径；本次文档核对本身不执行这些副作用。
