---
knowledge_id: "governance.maintenance"
knowledge_type: "guide"
status: "current"
summary: "定义按需知识扩展的元数据、更新流程和按变更选择的验证范围。"
aliases: ["如何更新文档", "知识过期", "代码文档同步", "frontmatter", "knowledge_id", "AI共治规范", "贡献指南", "CONTRIBUTING.md"]
code_paths: ["AGENTS.md", "CONTRIBUTING.md", "docs/AGENTS.md", "docs/.vitepress/scripts", "package.json"]
test_paths: ["docs/.vitepress/scripts/knowledge.test.mjs", "docs/.vitepress/scripts/readme-links.test.mjs", "docs/.vitepress/scripts/html-links.test.mjs"]
related: ["governance.knowledge", "governance.retrieval", "delivery.testing"]
---

# 知识维护规范

编辑 `docs/` 时使用本页；普通代码任务从 `AGENTS.md` 和所属主题开始，无需通读治理资料。维护目标是让新上下文按需取得可靠契约，减少重复事实和无效阅读，不以页数、篇幅或检索命中衡量效果。

## 单一事实位置

工作规则在 `AGENTS.md`，能力事实在权威主题，局部实现原因在代码旁，版本变化和本轮证据在 Git/CHANGELOG 或任务报告。按能力、责任边界和执行链组织；操作、排障与实现约束属于同一事实时不要另写受众或语言副本。

源码旁 README 是模块/包入口，链接权威主题；随包交付时保留运行前提与风险，不能只剩包内不可用的仓库链接。主题以简体中文维护，保留有用的原生 README 和精确符号。

地图、catalog、导航和网站均由元数据派生。侧栏仅提供检索、源码组和能力领域入口，完整源码关联留在地图；关联是复核线索，不代表主题所有权或调用图。`docs/_history/` 是仓库内历史存档，不进入目录、搜索、导航、构建或 HTML 校验，活动页面不要建立指向它的网页链接。

## 清晰、不冗余的主题结构

一个主题解释一项独立能力或契约，先交代用途与适用范围，再按实际需要给出理由、边界、步骤、默认值、异常语义、示例与验证。无需固定模板或每页套齐章节；保留有用知识，删除重复解释、失效入口和过程记录。

源码与验证入口跟随对应契约，命令前提和副作用贴近步骤。长主题先改善结构，只有独立用途才拆分；用链接连接边界，不复制上下游契约。当前事实原位更新，历史说明只保留仍有效的兼容或迁移理由。

## 元数据契约

每个活动主题保留以下八个平面字段。字符串用 JSON 双引号，数组用单行 JSON 字符串数组；字段与数组元素不重复，字符串和数组元素非空，允许空数组。已有 VitePress 字段可保留，知识字段不能嵌套。

```yaml
knowledge_id: "ui.property-grid"
knowledge_type: "topic"
status: "current"
summary: "属性编辑器的选择、实例复用、失败降级和扩展验证。"
aliases: ["自定义属性编辑器", "PropertyGrid", "IPropertyEditor"]
code_paths: ["UI/ColorVision.UI/PropertyEditor"]
test_paths: []
related: ["ui.index"]
```

| 字段 | 契约 |
| --- | --- |
| `knowledge_id` | 稳定且唯一，使用既有领域前缀；标题和 URL 变化不随意改 ID |
| `knowledge_type` | `index` 入口、`topic` 能力契约、`guide` 工作方法、`reference` 定位资料、`decision` 设计决策 |
| `status` | `current` 当前、`planned` 未落地、`historical` 回溯；默认搜索仅 current |
| `summary` | 一句话说明解决的问题及重要限制 |
| `aliases` | 真实能力、界面、配置、诊断词和关键符号；不机械列出每个方法或照抄验收问题 |
| `code_paths` | 已存在的仓库相对文件/目录，以 `/` 分隔、大小写准确；不使用 URL、本机路径、glob 或越界路径 |
| `test_paths` | 确实相关且已存在的验证文件，允许 `[]`；引用不代表运行或通过 |
| `related` | 必要上下游的有效知识 ID，不引用自身、不要求递归阅读全部关联 |

目录路径覆盖子路径，但不是完整依赖关系。源码/测试路径用元数据或行内代码，不能写成跨出 `docs/` 的网页链接。路径有效不证明描述正确；契约与实现冲突时调查，不能静默迁就其中一方。`current` 不表示默认启用或全部验证通过；实验门禁、未实现设计和证据缺口必须说明，不维护虚构或永远不更新的验证日期。

## 随代码变化更新

1. 核对请求范围与工作区；已知归属直接读主题和必要代码，未知归属用搜索或地图。
2. 判断是否改变公开行为、协议、默认值、架构边界、操作、构建/发布命令或已有引用。内部重构、局部优化或恢复既有契约，若事实与入口仍准确，无需改正文。
3. 只修正受影响的权威位置。归属仍不清或源码/测试移动、删除时，用 `impact` 查候选；没有候选也不机械新建主题。有独立用途的设计理由、兼容约束或反复出现的陷阱才值得新增。
4. 按下表生成和验证，检查最终差异，报告实际覆盖与缺口。不要逐页改写关联主题或追加本轮过程。

```powershell
# 只读；可查询已删除路径，返回复核候选
node docs/.vitepress/scripts/knowledge.mjs impact "UI/ColorVision.UI/PropertyEditor"
```

## 按变更选择验证

| 变更 | 本地生成与验证 | 网站验证 |
| --- | --- | --- |
| 纯代码，正文、元数据和引用均未变 | 相关代码验证；无需知识生成 | 无需本地网站构建 |
| 仅正文措辞或 AGENTS 规则，标题/元数据未变 | `npm run docs:check`；无需生成 | 普通正文可由文档 CI 构建；需立即验收页面时本地构建 |
| 标题、元数据、主题增删或生成规则 | `npm run docs:knowledge` 后 `npm run docs:check` | 导航或路由变化时 `npm run docs:build` |
| 页面链接、章节标题、资源、Markdown/Vue 结构、样式或站点配置 | `npm run docs:check`，目录输入变化才生成 | `npm run docs:build`；视觉/交互变化还需浏览器验证 |
| 检索、校验或生成工具 | 相关工具测试及 `npm run docs:check`，目录输入/规则变化才生成 | 影响网页、搜索索引或链接解析时 `npm run docs:build` |

检索规则或元数据变化时复测相关固定问题及预留的独立问题，方法见[检索验收](./retrieval-checks.md)。普通措辞调整不要求接手试验。保留现有 CI；未运行的检查说明原因，不能把“将由 CI 执行”报告成已通过。

`docs:knowledge` 写入版本化派生资料，不发布。catalog 记录元数据、标题与定位字段，不存正文全文哈希；标题/元数据未变的正文修改无需生成，也不要产生时间戳噪声。`docs:check` 只需 Node，检查元数据、路径、生成资料新鲜度、固定检索及工具回归，不核对已构建网页是否同步。

`docs:build` 另需网站依赖，包含上述检查、HTML 工具回归、本地构建、搜索索引及生成 HTML 校验，不上传。正文和网站全文搜索的同步由构建完成；网站搜索与 CLI 元数据搜索不同。

README 校验只扫描约定范围内源码旁 README 到 `docs/` 的链接：目标存在、大小写准确、不越界、页面未退役，目录有 Markdown 入口。支持常用行内、引用式和简单 HTML 链接，忽略 frontmatter、代码与注释；跳过隐藏/依赖/产物目录及目录符号链接，包含未跟踪 README。它不要求 README 元数据，不验证正文、源码/外链或目标章节；具体范围见 `docs/.vitepress/scripts/readme-links.mjs`。

HTML 校验以 Vue 解析器读取生成静态页面，不执行脚本；本地页面/章节和搜索锚点须命中真实 `id`，大小写与中文标点不得自行改写。代码、注释、惰性模板不提供目标，PDF/SVG 等资源只查存在，外链不联网。它不验证根 README 自身锚点、动态元素或浏览器操作。修改校验器在隔离临时仓库/站点覆盖缺页、兼容页、大小写、错锚点、路径及符号链接边界，不能改坏真实产物或忽略解析错误来获得通过。

## 授权、保密和验证分层

示例不扩大授权。明确区分安装依赖、本地构建、启动应用/设备、改数据库、删除数据和签名上传，副作用及前提写在命令旁；打包上传不能用于普通文档验证。正文不存账号密码、令牌、用户数据库、本机私有路径或未经授权的客户样本，日志/协议示例用脱敏或合成数据。

字段/路径、检索路由、源码/行为、真机/交付分别是不同证据；任何一层通过不能替代其余层。

## 迁移和删除

合并主题后更新正文链接、`related` 和入口。旧 URL 有兼容价值时保留短页并设置 `redirect_from_deleted_page: true`、`search: false`，否则删除；有回溯价值的主题标为 `historical`。删除重复入口前，将唯一的前提、风险、完成判据和故障知识迁到所属主题。不要新增手工模块表、第二份导航或语言镜像。

## 参考依据与边界

工具行为以 `docs/.vitepress/scripts/` 与对应测试核对；问答质量需按[检索验收](./retrieval-checks.md)抽样。生成或构建通过不是当前事实正确、AI 自动读入或接手效率提升的证明。
