---
knowledge_id: "governance.retrieval"
knowledge_type: "guide"
status: "current"
summary: "定义本地知识查询的 CLI、匹配契约和独立问题抽样方法，供检索工具维护时使用。"
aliases: ["文档验收", "冷启动", "检索测试", "retrieval", "新用户问Codex"]
code_paths: ["docs/.vitepress/scripts"]
related: ["governance.maintenance", "delivery.testing"]
---

# 知识检索与问答验收

维护知识查询或评估交接时使用本页。查询帮助找到专题，实际行为沿源码和调用方核实。

## 自动检查

`npm run docs:check` 校验目录、固定检索和工具回归；`npm run docs:build` 还构建本地网站、搜索索引并验证 HTML 链接。依赖、生成时机和检查边界见[维护规范](./maintenance.md#按变更选择验证)。两者均不发布或启动 ColorVision。

固定检索用例按实际查找问题补充，定期删减过期、重复或失去回归价值的条目。

## 检索规则的回归边界

CLI 查专题元数据，网站搜索使用页面全文。优先用实际界面名、代码符号或路径。返回顺序以 CLI 为准。`owner-fallback` 只定位可能的所属类型或路径，成员和版本仍须在源码核对。匹配细节以 `knowledge.mjs` 中的 `searchCatalog` 为准。

### CLI 数量与读取范围

```powershell
node docs/.vitepress/scripts/knowledge.mjs search "StateStore.Save" --limit 5
node docs/.vitepress/scripts/knowledge.mjs search "ONNX" --all --limit 5
node docs/.vitepress/scripts/knowledge.mjs impact "UI/ColorVision.UI/PropertyEditor"
```

`search` 默认返回 12 条当前主题。`--limit N` 截断结果，`--all` 纳入规划和历史主题。`--` 后按字面查询。`search: false` 的主题始终排除。

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

顶层只有 `total`、`matches`，每项只有上述六个字符串字段。按数组顺序读取，源码关联到主题核对。零匹配返回 `{"total":0,"matches":[]}`。参数错误、catalog 缺失或损坏时退出 1，stderr 写文本错误，stdout 为空。先检查退出码再解析。`--help` 保持文本帮助。

Windows PowerShell 遇到中文乱码时，可在当前会话指定 UTF-8。

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$json = node docs/.vitepress/scripts/knowledge.mjs search "knowledge" --json --limit 5
if ($LASTEXITCODE -ne 0) { throw "Knowledge search failed." }
$result = $json | ConvertFrom-Json
$result.matches | Select-Object knowledge_id, source
```

### 修改检索工具时

真实任务的前五项缺少正确主题时，先核对归属、标题、摘要和实际别名。元数据调整仍无法解决时，再改排序。对照时固定问题和目录，复测已有回归和未加入回归的真实问题。

## 冷启动抽样条件

重要规则调整或实际交接时，从同一版本和独立上下文开始，给出真实任务和权限，保留源码定位、验证证据和卡住的地方。比较效果时固定代码、任务、工具和权限，多次独立抽样。

## 记录与失败处理

任务报告记录版本、入口、误判、缺失证据及无法验证的环境条件。过期契约在所属主题原位修正。执行命令前核对实际前提和授权。
