---
knowledge_id: "governance.knowledge"
knowledge_type: "index"
status: "current"
summary: "说明 AGENTS.md 的按需知识扩展、权威主题与生成资料各自的职责和入口。"
aliases: ["文档怎么用", "AI共治", "AI知识库", "AGENTS.md", "documentation", "版本历史入口", "CHANGELOG.md"]
code_paths: ["README.md", "AGENTS.md", "CHANGELOG.md", "docs/AGENTS.md", "docs/.vitepress/config.mts", "docs/.vitepress/scripts"]
related: ["governance.maintenance", "governance.retrieval", "platform.system", "platform.license"]
---

# 仓库知识使用约定

`docs/` 是根目录和各模块 `AGENTS.md` 的按需知识扩展，网站直接展示同一套 Markdown。

## 从问题到实现

已有类名、界面文字、报错或协议字段时，用 `rg` 找实现，沿调用核对测试。专题补充设计理由、兼容约束和现场语义。代码入口不清楚时，可在仓库根目录用下面的命令，也可查知识地图。 [知识地图](./knowledge/index.md)

```powershell
node docs/.vitepress/scripts/knowledge.mjs search "问题或代码符号" --limit 5
```

查询只需 Node，默认查当前主题。查规划或历史时加 `--all`。 [JSON 查询结果](./knowledge/retrieval-checks.md#json-查询结果)

## 资料职责

工作规则在 `AGENTS.md`，能力契约和实现入口在所属主题，测试随实现查找。模块和安装包的入口在各自 README。地图、catalog 和导航由主题元数据生成。版本变化查 `CHANGELOG.md`、Git 或仓库内的 `docs/_history/`。

## 常用入口

- [环境与构建前提](./00-getting-started/prerequisites.md) · [测试与验证](./02-developer-guide/testing.md)
- [系统职责与边界](./03-architecture/overview/system-overview.md)
- 编辑主题查[维护规范](./knowledge/maintenance.md#按变更选择验证)。改检索工具或做问答抽样查[检索验收](./knowledge/retrieval-checks.md)。
