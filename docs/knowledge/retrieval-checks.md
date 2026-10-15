---
knowledge_id: "governance.retrieval"
knowledge_type: "guide"
status: "current"
summary: "定义本地知识查询的 CLI、匹配契约和独立问题抽样方法，供检索工具维护时使用。"
aliases: ["文档验收", "冷启动", "检索测试", "retrieval", "新用户问Codex"]
code_paths: ["docs/.vitepress/scripts", "docs/knowledge/retrieval-cases.json"]
test_paths: ["docs/.vitepress/scripts/knowledge.test.mjs", "docs/.vitepress/scripts/readme-links.test.mjs", "docs/.vitepress/scripts/html-links.test.mjs"]
related: ["governance.maintenance", "delivery.testing"]
---

# 知识检索与问答验收

修改查询工具、检索元数据或评估知识接手效果时使用本页，普通任务无需通读。查询只帮助定位主题，源码问答另行核对行为；前五名命中和网站构建通过均不代表答案正确。

## 自动检查

`npm run docs:check` 校验目录、固定检索和工具回归；`npm run docs:build` 还构建本地网站、搜索索引并验证 HTML 链接。依赖、生成时机和检查边界见[维护规范](./maintenance.md#按变更选择验证)。两者均不发布或启动 ColorVision。

固定问题及允许主题 ID 在 `docs/knowledge/retrieval-cases.json`：每题前五项至少命中一个允许主题；`include_planned: true` 同时纳入 planned 和 historical。产品答案只在功能主题维护，验收结果留在任务报告。源码地图只展开元数据关联，宽泛根路径不扩散到所有子模块；关联不是所有权或事实正文副本。

## 检索规则的回归边界

本地 `search` 只读 catalog 元数据，不读全文或源码；网站搜索来自构建后的页面。查询大小写不敏感，Windows 分隔符转为 `/`；中文相邻双字片段及紧贴中文的符号参与匹配，下划线、C++ `::` 和路径边界保持完整。排序依次比较：

| 优先级 | 匹配依据 |
| --- | --- |
| 1 | 整条查询精确等于知识 ID、标题或别名 |
| 2 | 完整限定符命中数，其次是 ID/标题/别名明确声明的完整身份数 |
| 3 | 所属类型/版本前缀回退数、具体程度和描述命中 |
| 4 | 非限定代码符号完整命中数、明确身份数和描述命中 |
| 5 | 单句纯中文查询的完整主题对象匹配及标题主段支持 |
| 6 | 词法分，再按稳定知识 ID 顺序 |

重复符号不叠加计数，完整成员优先于 owner 回退；`score` 只是部分排序依据，消费者不能单独按它重排。

`Namespace.StateStore.Save` 可回退到 `Namespace.StateStore` 或 `StateStore`，不拆成 `Save` 或外层 `Namespace`。路径可回退具体尾部路径/文件名，不泛化到目录名。`widget-v1.2.3` 可回退已声明的 `widget-v`，数字组件不作为类型 owner。此类结果标记为 `owner-fallback`，不校验成员是否存在或版本格式是否合法。

单句纯中文查询在“没有”“无法”“是否”“怎么”等谓词前取完整对象；“一个”“一种”等新增类请求先去掉请求前缀，无谓词时取剩余整段。标题/别名须在完整词边界覆盖整个对象，且至少一个标题主段支持对象的开头短语，才提升候选；同名候选比较各自主段支持，不能仅截取开头泛词。标题主段止于冒号、顿号、逗号、分号或左括号。对象不明、缺少佐证、包含代码符号或内部标点时仍用其他依据；词边界由 Node `Intl.Segmenter` 提供，运行时分词数据和目录变化可能影响结果。此匹配标为 `text`，不证明语义理解或唯一归属。

### CLI 数量与读取范围

```powershell
node docs/.vitepress/scripts/knowledge.mjs search "StateStore.Save" --limit 5
node docs/.vitepress/scripts/knowledge.mjs search "ONNX" --all --limit 5
node docs/.vitepress/scripts/knowledge.mjs impact "UI/ColorVision.UI/PropertyEditor"
```

`search` 默认 12 项；`--limit N`、`--limit=N` 只截断，不改排序，文本输出会说明已显示和总匹配数。N 是正安全整数，重复 limit 或未知选项报错，`--` 后为字面查询。默认仅 current；`--all` 纳入 planned/historical，仍排除 `search: false`。

`impact` 接收一个仓库相对路径，返回全部相交主题，不接受搜索专用选项；已删除路径也可查询。结果是复核候选，不是完整依赖图。

### JSON 查询结果

`search --json` 可与 `--all`、两种 limit 写法组合，选项可放查询前后；无值开关，`--` 后的同名文本仍是查询内容。成功退出 0，stdout 只有一个 UTF-8 JSON 对象及末尾换行，stderr 为空：

| 字段 | 类型与含义 |
| --- | --- |
| `total` | 非负整数；状态/可搜索过滤后、截断前的总匹配数 |
| `matches` | 保持搜索排名的数组，长度不超过 limit；小于 total 表示截断 |
| `matches[].knowledge_id` | 字符串，稳定知识 ID |
| `matches[].status` | 字符串，current、planned 或 historical |
| `matches[].title`、`matches[].summary` | 字符串，标题和摘要 |
| `matches[].source` | 字符串，使用 `/` 的仓库相对 Markdown 路径 |
| `matches[].match_kind` | 字符串，exact、qualified-symbol、owner-fallback、code-symbol 或 text |

顶层只有 `total`、`matches`，每项只有上述六个字符串字段，不暴露分数或完整 metadata。按数组顺序读取，源码/测试关联到主题核对。零匹配仍成功，返回 `{"total":0,"matches":[]}`，空白排版不是契约。参数错误、catalog 缺失/损坏退出 1，stderr 写文本错误，stdout 为空，不返回 JSON 错误对象。先检查退出码再解析；`--help` 保持文本帮助。

Windows PowerShell 若按本地代码页解码中文，可仅为当前会话指定 UTF-8：

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$json = node docs/.vitepress/scripts/knowledge.mjs search "knowledge" --json --limit 5
if ($LASTEXITCODE -ne 0) { throw "Knowledge search failed." }
$result = $json | ConvertFrom-Json
$result.matches | Select-Object knowledge_id, source
```

## 非限定代码符号与混合问句

`searchCatalog` 从可搜索主题的标题、别名、摘要及源码/测试路径的原始拼写识别内部大小写边界、`HImage`/`XMLReader` 或 snake_case 形态。识别后大小写无关，`StateStore`、`statestore`、`STATESTORE` 排序一致。`Save`、`backup`、`ID` 等普通词/缩写无此优先级；查询本身大写、长名称或相邻下划线不能冒充完整符号。

形态不证明类型身份，`SQLite` 等技术名也可能满足条件。混合查询中，若普通 ASCII 词在更少主题完整命中，并找到不含某代码词的竞争主题，较广泛的代码词只计词法分，例如 `cvsln ... SQLite`。频率在状态过滤后按主题计一次；未知词、数字及限定 token 不参与抑制，每个代码词独立判断，稀有词仅出现在同一类型主题时不构成竞争。目录变化可能影响启发式排序；`code-symbol` 不是唯一归属或源码真实性证明。

### 修改检索工具时

1. 先保存未加入固定用例的真实问题、允许主题、catalog 和原始结果。算法复测期间冻结问题与目录，区分匹配变化和元数据补充。
2. 在隔离合成目录覆盖限定符、owner、路径、版本前缀、非限定符、中文噪声/对象、大小写、边界、多符号去重和确定性顺序，保留未知成员、纯数字、普通词及稀有业务词竞争的反例。
3. CLI 回归从其他工作目录运行，只复制脚本与合成 catalog，不装网站依赖。覆盖数量/状态过滤、字段/转义/顺序/截断、零匹配、字面选项及错误通道；损坏正文或缺失源码不妨碍查询，查询不写 catalog，帮助不依赖 catalog。
4. 运行固定用例并复测预留问题，记录未改善结果；别名来自真实名称，不能照抄问题后宣称通用效果。新风险补有区别能力的回归。

纯中文及中英文符号混合问题分别抽样；加入固定用例后不能再算独立样本。隔离回归不是所有 Node 版本的首次克隆验收，工具回归通过也不是 AI 问答质量证明。

## 冷启动抽样条件

重要规则调整或实际交接时才做接手抽样，普通编辑不设此门禁。使用同一已提交版本的独立副本和新上下文，从仓库根入口开始，不给答案文件、正确主题、聊天历史、个人记忆或未提交知识。开始前固定问题、修改范围、工具、权限与验收条件；解释、小修复、小扩展可分别抽样，问答不要求构建，试验补丁单独保留、不自动合并。

记录实际读取、误判、缺口、无关阅读和维护步骤；环境/依赖/设备或样本不可用单列，不能归咎于知识，也不推测 token 数。要比较收益，固定代码、任务、模型/工具和权限并重复独立取样；单例、命中或减少读取文件不能推算净效率。

只读提示示例：

> 遵守仓库 AGENTS.md，定位主题并核对必要源码和测试，回答问题并给出依据、适用范围与未验证事项。不修改文件、运行发布脚本或操作真实设备。

## 代表性问题与语义要求

按风险轮换问题，不在本页复制模块答案：

| 维度 | 示例问题 | 核对重点 |
| --- | --- | --- |
| 扩展 | 如何新增属性编辑器？ | 发现、选择、生命周期、失败及测试 |
| 分层 | 新增算法结果如何叠加显示？ | 输入、结果类型、注册与显示链 |
| 完成 | `RunAll Code=0` 是否完成？ | 返回值、事件与最终结果语义 |
| 保存 | 关闭设置窗口是否撤销选项？ | 编辑对象、提交/保存时机及异常 |
| 状态 | ONNX 现在能直接跑吗？ | status、实现、开关及交付条件 |
| 副作用 | 项目包怎样本地构建？ | 调用链、复制/上传边界及前提 |

未知成员、失效入口或不存在的前提要明确指出，不能从近似名称补造 API；契约与代码冲突要记录并调查。

## 记录与失败处理

报告记录版本/工作区、问题、入口、实际读取、正确/错误之处及未验证范围，不追加到产品正文，也不设固定调用次数门槛。

| 现象 | 处理 |
| --- | --- |
| 找不到/找错层 | 检查真实名称、归属与摘要，区分元数据和匹配问题，避免新增重复总览 |
| 过期 | 原位修正契约/关联并补必要回归 |
| 无法验证 | 说明环境、设备或样本缺口 |
| 命令有副作用 | 核对现有授权与前提，只读问答不执行 |

抽样只发现薄弱点，不认证所有模型、问题或环境。
