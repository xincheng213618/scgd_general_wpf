---
knowledge_id: "algorithms.json-templates"
knowledge_type: "reference"
status: "current"
summary: "JSON模板的文本/属性编辑、数据库保存、默认参数与重置；校验Json按钮只同步模型，Schema提供字段提示而不补默认值或执行完整校验。"
aliases: ["JSON模板","JSON模板保存和V2结果如何对应","属性编辑","文本编辑","校验Json","设置为默认参数","无法重置，请检查数据库相关配置","JSON Schema默认值","Schema default","JSON模板重置","HDR参数Schema","ITemplateJson","TemplateJsonParam","EditTemplateJson","JsonPropertyEditorControl","JsonEditorSchemaDocument","CanHandle1","SchemaIndexResourceName","TryLoadTemplateSchema"]
code_paths: ["Engine/ColorVision.Engine/Templates/Jsons","Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Templates/HDR/TemplateHDR.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Templates/HDR/Camera.RunParams.schema.json","UI/ColorVision.UI/PropertyEditor/Json","UI/ColorVision.UI/Utilities/JsonHelper.cs","UI/ColorVision.Common/Utilities/DebounceTimer.cs"]
test_paths: []
related: ["algorithms.index","algorithms.template-management","engine.host","engine.template-design","engine.results","copilot.tool-contracts"]
---

# JSON 模板

JSON 模板将一条参数保存为数据库 `ModMasterModel.JsonVal`，由 `ITemplateJson<T>` 加载和持久化。本文说明通用 `EditTemplateJson` 编辑器、默认参数、Schema 元数据和存储限制。KB 等模板可以使用专用编辑器，不能直接套用通用界面的行为。

模板窗口的入口、选择、创建来源和取消语义见[模板编辑与创建宿主](./template-management.md)；模板注册与数据库身份见[模板注册、参数与持久化](../../../03-architecture/components/templates/design.md)。编辑已有参数、创建模板和设置默认参数都依赖对应的 MySQL 数据及字典；Schema 文件不替代数据库。

## 编辑并保存参数

1. 在对应模板窗口选择一条参数。通用编辑器默认使用属性模式，之后记住上次模式；底部“文本编辑”或“属性编辑”用于切换到另一种模式。
2. 在属性模式按参数名称、路径或值搜索，展开对象或数组后编辑。字符串和数值输入框在失去焦点时提交，布尔值和枚举在选择变化时提交。需要增加字段、修改空数组或复杂原始载荷时，使用文本模式。
3. 文本模式修改后稍候再保存；它通过 50 ms 防抖把内容写入参数对象。对象 `{...}` 和数组 `[...]` 可通过 `JsonValue` 的语法检查，单个字符串、数字等顶层标量不能；属性模式只接受对象。
4. 确认修改已经进入参数对象，再使用宿主“保存”或 `Ctrl+S` 写数据库。编辑器内的“校验Json”不执行保存。批量保存和关闭的限制见下文。

界面只有文本和属性两种视图。模板类传入的 `Description` 不是第三种注释视图；字段说明来自可选 Schema。

### 校验、重置和辅助入口

| 入口 | 实际作用 |
| --- | --- |
| **校验Json** | `CheckCommand` 触发 `JsonValueChanged`，编辑器从当前参数对象重写文本；不执行完整 Schema 校验，也不调用算法。无效文本草稿没有进入模型时，点击会用上次有效内容覆盖草稿 |
| **重置** | 按当前记录的 `Pid` 查询字典 `SysDictionaryModModel.JsonVal`，尝试替换内存参数；需要另行保存才写回当前模板。字典缺失或没有字符串默认值时提示“无法重置，请检查数据库相关配置”；默认字符串语法无效时，参数 setter 拒绝赋值并保留当前值 |
| **json** | 用系统浏览器打开 `json.cn`，并把当前模型 JSON 放入剪贴板；代码不自动粘贴或上传文本 |
| **询问Copilot** | 将当前模板上下文交给 Copilot；解释与参数建议快捷项直接发起任务，“发送到Copilot”只预填。补丁预览、应用和保存是不同操作，见[模板 JSON 预览与应用](../../../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md#模板-json-预览与应用) |

### 当前同步限制

- **重置后属性区可能仍显示旧值。** 重置和“校验Json”的事件只刷新文本，不重建属性区；继续改旧属性会把旧对象内容写回模型。需要重置时先切到文本模式，再重置并确认内容，最后切回属性模式。
- **解析失败不保证清空旧属性。** `JsonPropertyEditorControl.SetJson` 在解析新对象失败时保留上次对象及控件，显示错误提示。此时不要把仍可见的旧字段当成新文本已加载；再次切回文本可能取回旧对象，覆盖草稿。
- **快速保存或多个编辑器会遇到防抖边界。** 所有实例共用 `EditTemplateJsonChanged` 计时器键，后一次输入可以取消另一个实例的待处理回调；回调读取执行时的当前参数和文本。保存、切换模板、卸载控件没有显式冲刷或取消机制，不能保证尚未提交的输入已保存。

这些是当前实现的缺口，不是预期的数据丢失契约。修改同步链时应验证上述场景，不能以切换视图未抛异常或“保存成功”提示代替内容核对。

## 默认参数从哪里来

| 来源 | 用途和影响范围 |
| --- | --- |
| 字典 `SysDictionaryModModel.JsonVal` | 普通新建默认值按 `TemplateDicId` 读取；“重置”按当前记录 `Pid` 读取。复制、导入等创建来源可以覆盖新建内容 |
| 当前 `ModMasterModel.JsonVal` | 当前模板的参数；有效编辑先改变内存对象，保存才调用数据库更新 |
| Schema 的 `default` | 维护时记录的字段/对象默认值元数据；编辑器不据此初始化模板、补齐缺失字段或执行重置 |
| 模板类的 `Description` | 不参与通用编辑器默认值计算，也不自动变成 JSON 注释 |

要让当前内容成为后续新建或重置的默认值，在模板列表选择目标，使用“设置为默认参数”并确认。该操作在一个事务中更新对应字典与当前模板记录，不只是修改本地配置，也不批量替换其它已有模板。确认前应完成输入提交并核对目标；搜索过滤后的索引限制见[搜索后的显示位置不总是源位置](./template-management.md#搜索后的显示位置不总是源位置)。

## Schema 提供哪些能力

属性控件根据**实际 JSON 中已有字段及其值类型**生成；Schema 用于补充说明和部分交互约束。`JsonEditorSchemaDocument` 是元数据解析器，不是完整 JSON Schema 验证器。

| 字段或机制 | 通用属性编辑器的处理 |
| --- | --- |
| `properties`、`items` | 按对象路径和数组路径匹配说明；不会创建 JSON 中不存在的字段 |
| `title`、`description`、`unit`、`examples` | 参数标签、提示和检索信息 |
| `enum`、`x-enumDescriptions` | 枚举选择及说明；已有值不在枚举中时可显示为未选中，不会自动改成第一项 |
| `minimum`、`maximum` | 数值输入框失去焦点时检查范围；不扫描文本模式的所有值，也不逐项验证简单数组 |
| `x-provider.jsonPath`、`x-colorvision` 来源信息 | 路径匹配与来源/维护状态说明；不是 DLL 版本或参数兼容性验证 |
| `default`、`type`、`required`、`additionalProperties`、`exclusiveMinimum` / `exclusiveMaximum`、`$ref` | 不作为完整约束执行；不补默认值、不解析外部引用，输入控件类型仍来自现有 JSON 值 |
| `x-ui.group` / `order` / `advanced` | 当前控件不消费这些分组、排序或高级选项声明 |

没有 Schema 时仍可按现有对象编辑；Schema 中未声明的普通字段留在对象中，不会因缺少声明被自动删除。不过以下输入不具备任意 JSON 往返保证：

| 输入 | 当前限制 |
| --- | --- |
| 整数输入框 | 用 `Int32` 解析；较大整数应在文本模式维护 |
| 简单数组 | 用逗号分隔文本，按原数组首项类型解析全部项目；空项及无法解析的数值/布尔值会被跳过，字符串中的逗号会被拆开。混合类型或含逗号字符串应在文本模式维护 |
| 空数组、`null` | 属性模式没有可直接补内容的编辑器，使用文本模式 |
| 字段名本身含 `.` 或 `[...]` | 更新逻辑把这些符号当嵌套路径，不能保证写回原字面键；使用文本模式 |

`JsonPropertyEditorControl.ValidateJson()` 也只是对当前输出做对象语法解析，且没有接到 Engine 的“校验Json”按钮。语法有效不代表字段完整、范围全部合规或算法可执行。

## Schema 查找与发布边界

`EditTemplateJson.TryLoadTemplateSchema` 按当前 `TemplateJsonModel.Code` 查找，Code 为空时不加载；字典 ID 和算法结果版本不参与匹配。

1. 先读当前 Engine 程序集的 `Templates/Jsons/Schemas/schema-index.json`，在 `schemas` 中按忽略大小写的 `code` 匹配，再按条目 `file` 读取嵌入资源。
2. 嵌入资源未命中才查磁盘：分别从 `AppContext.BaseDirectory`、`Environment.CurrentDirectory` 向上最多八层，在每层尝试 `Templates/Jsons/Schemas/schema-index.json` 和 `Engine/ColorVision.Engine/Templates/Jsons/Schemas/schema-index.json`。
3. 磁盘条目的 `file` 相对索引所属的 `Jsons` 目录解析。路径去重后逐个尝试；文件缺失、索引解析/读取失败或 Code 未匹配时继续下一候选，全部未命中则不提供 Schema。

查找阶段只解析索引，不验证 Schema 内容。非空但无效的嵌入 Schema 仍会优先返回，后续解析失败不会重新触发磁盘回退。工程嵌入规则见 [Engine 资源打包](../../engine-components/ColorVision.Engine.md#资源不是同一种输出文件)：输出目录没有散文件不等于漏包，开发机磁盘回退成功也不能证明交付程序集正确。

**HDR 有明确的路径缺口。** `TemplateHDR`（字典 `43`、Code `Camera.RunParams`）仍在 `Services/Devices/Camera/Templates/HDR/` 使用通用 JSON 编辑器，Schema 也在该目录；索引的 `HDR/Camera.RunParams.schema.json` 却相对 `Templates/Jsons/` 解析，该位置没有文件，且实际位置不在工程 `Templates/Jsons/**/*.schema.json` 嵌入范围内。按当前源码构建不能保证该条目被正常加载。排查应核对索引路径与嵌入资源，不应把它解释为 HDR 功能不存在；普通相机参数也使用 `Camera.RunParams` Code，Code 本身不足以区分两类模板。

### 维护字段说明

在对应算法目录维护 Schema，在 `Schemas/schema-index.json` 登记 Code 和相对路径。保留算法约定的 JSON 字段名；只改善标题或单位不应重命名参数。字段片段例如：

```json
{
  "title": "最小面积",
  "description": "参与检测的最小连通区域面积。",
  "type": "integer",
  "minimum": 0,
  "unit": "px"
}
```

供应方 `defaultparam.txt`、Schema 中的 `default` 和数据库字典是不同载体。Schema 的来源标记说明维护时的快照，不会自动跟踪 DLL 或数据库；修改默认参数后应核对对应字段、类型和默认值。保留 `additionalProperties: true` 以表达可扩展字段意图，但当前编辑器并不执行该关键字。`$comment` 和 provider/AI 指导文字不构成运行时校验或新的操作授权。

## 数据库保存、导入导出和删除

| 操作 | `ITemplateJson<T>` 的实际边界 |
| --- | --- |
| 加载 | 清 `SaveIndex`，查询本字典下未删除的主记录；按 ID 复用包装项、替换参数或追加新项。不移除数据库已不存在的旧集合项；离线时不恢复旧值 |
| 保存 | 逐个更新 `SaveIndex` 中仍有效的记录。选择过的条目即可进入该集合，不等于只保存实际改动项；没有整个批次的事务或受影响行数确认 |
| 复制、导出 | 序列化参数对象；`TemplateJsonModel` 被 `JsonIgnore` 排除，参数的 `JsonValue` 参与序列化。单项导出 `.cfg`，多项导出包含 `.cfg` 的 ZIP；净化后的同名文件可能相互覆盖 |
| 导入、新建 | 文件导入读取 UTF-8 `.cfg` 并反序列化为 `T`，准备创建来源，尚未插入数据库。反序列化为 `null` 也可返回成功；不能凭布尔返回值认定载荷完整。最终新建以目标字典重建 `Code`、`Pid` 并使用新名称，不是恢复源数据库身份 |
| 删除 | 按主记录 ID 物理删除；多选分支逐项写数据库，但移除界面项时复用同一个集合索引，可能移除错误项或越界，不能视为可靠的原子批量删除 |

宿主关闭会调用 `Load()`，但这不等于完整回滚；共享对象、离线和旧集合项的限制仍然存在。删除、导出、设置默认等动作还要核对当前勾选集合与过滤后的显示索引，见[模板宿主选择规则](./template-management.md#搜索后的显示位置不总是源位置)。数据库操作出现异常时应先核对已写入的记录，不能假设整批都没有生效。

## 常见模板标识与结果版本

以下标识用于定位常见模板，完整身份以具体模板构造与目标数据库字典为准。字段结构由对应算法约定，不能由“V2”目录名推断。

| 模板 | TemplateDicId / Code |
| --- | --- |
| LED 点阵 `LedCheck2` | `18` / `FindLED` |
| LED 灯条 `LEDStripDetectionV2` | `26` / `LEDStripDetection` |
| OLED AOI | `28` / `OLED.AOI`；四合一、复判、黑屏分别使用字典 `55`、`56`、`57` 的独立 Code |
| 双目融合 | `35` / `ARVR.BinocularFusion` |
| SFR 找 ROI | `36` / `ARVR.SFR.FindROI` |
| BlackMura | `37` / `BlackMura.Caculate` |
| Ghost / FOV / Distortion | `38` / `ghost`，`39` / `FOV`，`40` / `distortion` |
| AA 构建 POI / 找点 | `41` / `BuildPOI`，`42` / `FindLightArea` |
| 相机 HDR | `43` / `Camera.RunParams`，位置与 Schema 缺口见上文 |
| POI 分析 / 十字计算 | `44` / `PoiAnalysis`，`45` / `FindCross` |
| MTF / SFR | `48` / `MTF`，`49` / `SFR` |
| 图像 ROI | `52` / `Image.ROI`，与强类型裁剪模板不同 |
| KB | `150` / `KB`，使用专用编辑器 |

同名算法可同时存在 JSON 与强类型模板。结果显示失败时，沿实际模板、MQTT 事件名、结果 `Version`、`ViewHandle*.CanHandle1` 逐项检查；Schema 索引中的版本不是结果 handler 的匹配条件。结果发现与分发统一见[结果处理链](../../engine-components/result-handoff-chain.md)。

## 验证入口与缺口

目前没有声明直接覆盖 `ITemplateJson`、`TemplateJsonParam`、`EditTemplateJson` 或属性 Schema 控件的自动化测试。流程包测试不能替代这些编辑、数据库与控件同步检查。

修改此链路时，应在隔离数据中验证有效/无效文本、顶层数组、属性失焦、重置、连续切换、多窗口防抖、保存后重新读取，以及大整数/简单数组/字面路径键等边界。Schema 另核对 Code、索引、实际文件、程序集逻辑资源名及无 Schema 回退；导入导出需比较有效载荷和目标字典身份，结果链需实际事件/版本样本。源码审查和文档构建通过不代表上述运行行为已经验证。
