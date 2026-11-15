---
knowledge_id: "governance.maintenance"
knowledge_type: "guide"
status: "current"
summary: "定义按需知识扩展的元数据、更新流程和按变更选择的验证范围。"
aliases: ["如何更新文档", "知识过期", "代码文档同步", "frontmatter", "knowledge_id", "AI共治规范", "贡献指南", "CONTRIBUTING.md"]
code_paths: ["AGENTS.md", "CONTRIBUTING.md", "docs/AGENTS.md", "docs/.vitepress/scripts", "package.json"]
related: ["governance.knowledge", "governance.retrieval", "delivery.testing"]
---

# 知识维护规范

先定位实现。行为或外部契约有变化时，更新所属专题。普通代码任务按根 `AGENTS.md` 工作。

## 单一事实位置

同一契约由所属主题维护，其他页面链接过去。变更与验证记录留在 Git、CHANGELOG 或任务报告。`docs/_history/` 供仓库内回溯，活动页面用行内代码指向该目录。

## 清晰、不冗余的主题结构

只记录影响决策的理由、跨模块契约、兼容条件和现场操作。能直接从实现看出的内容，留在代码。

### 正文与代码的分工

源码映射优先使用稳定的实现入口。代码内部默认值、调用顺序、重试和日志字段按需查看。对操作、兼容或业务判定有影响的值才写进契约。测试随实现定位。

## 元数据契约

每个活动主题保留以下七个平面字段。字符串用 JSON 双引号，数组用单行 JSON 字符串数组。字段与非空字符串元素均须唯一，允许空数组。可保留已有 VitePress 字段。

```yaml
knowledge_id: "ui.property-grid"
knowledge_type: "topic"
status: "current"
summary: "属性编辑器的选择、实例复用、失败降级和扩展验证。"
aliases: ["自定义属性编辑器", "PropertyGrid", "IPropertyEditor"]
code_paths: ["UI/ColorVision.UI/PropertyEditor"]
related: ["ui.index"]
```

| 字段 | 契约 |
| --- | --- |
| `knowledge_id` | 稳定且唯一，使用既有领域前缀。标题或 URL 变化时保持原 ID |
| `knowledge_type` | `index` 入口、`topic` 能力契约、`guide` 工作方法、`reference` 定位资料、`decision` 设计决策 |
| `status` | `current` 当前、`planned` 未落地、`historical` 回溯。默认搜索仅 current |
| `summary` | 一句话说明解决的问题及重要限制 |
| `aliases` | 真实能力、界面、配置、诊断词和关键符号。验收问题留在检索用例 |
| `code_paths` | 仓库内已存在的源码文件或目录。使用 `/`、准确大小写和相对路径 |
| `related` | 必要上下游的有效知识 ID，不引用自身、不要求递归阅读全部关联 |

目录路径用于匹配子路径。仓库源码用元数据或行内代码表示，网页链接留给 `docs/` 内的页面。

## 随代码变化更新

仅修正已变化的契约与源码引用。归属不清时用 `impact` 查候选，再核对调用者。

## 按变更选择验证

| 改动 | 本地检查 |
| --- | --- |
| 代码改动且契约、引用仍准确 | 按代码改动选验证 |
| 仅改正文措辞、协作规则或检索工具 | `npm run docs:check` |
| 改主题标题、元数据、增删页面或生成规则 | `npm run docs:knowledge` 后执行 `npm run docs:check` |
| 改链接、章节锚点、导航、离线帮助或站点行为 | `npm run docs:build`，视觉或交互变化再用浏览器确认 |

`docs:check` 只需 Node。`docs:build` 另需网站依赖，并检查离线帮助、全文索引和站点链接。两者均本地执行。检索变更另复测真实问题。 [检索验收](./retrieval-checks.md)

## 授权、保密和验证分层

日志和协议示例用脱敏或合成数据。报告列明实际完成的检查及缺口，分别记录检索、源码/行为、真机和交付结果。

## 迁移和删除

合并前迁移仍有用的前提、风险、完成判据和排障信息。合并后更新链接、`related` 和入口。仅在旧 URL 需要兼容时保留短跳转页，设置 `redirect_from_deleted_page: true` 和 `search: false`。有独立回溯价值的主题标为 `historical`。

## 参考依据与边界

工具行为以 `docs/.vitepress/scripts/` 和测试为准，AI 接手效果用实际任务独立抽样。[检索验收](./retrieval-checks.md)
