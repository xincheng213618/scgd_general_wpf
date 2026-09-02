---
knowledge_id: "governance.knowledge"
knowledge_type: "index"
status: "current"
summary: "说明仓库知识入口、按需检索、源码核对和文档与代码同步维护的共同规则。"
aliases: ["文档怎么用", "AI共治", "AI知识库", "AGENTS.md", "documentation", "版本历史入口", "CHANGELOG.md"]
code_paths: ["README.md", "AGENTS.md", "CHANGELOG.md", "docs/AGENTS.md", "docs/.vitepress/scripts"]
test_paths: ["docs/.vitepress/scripts/knowledge.test.mjs"]
related: ["governance.maintenance", "governance.retrieval", "platform.system", "platform.license"]
---

# 仓库知识使用约定

`docs/` 是 ColorVision 的版本化知识层，不是必须顺序阅读的手册。AI 从根目录和最近的 `AGENTS.md` 取得工作规则，再按问题读取主题、关联源码和测试；网页只是同一正文的派生展示。

## 从问题到实现

1. 从[知识地图](./knowledge/index.md)按源码职责定位，或直接使用本地查询。
2. 阅读最小相关主题及必要的 `related`，区分 `current`、`planned` 和验证缺口。
3. 核对当前分支源码与测试；文档或实现冲突时报告并调查。
4. 行为、契约、架构或命令改变后，用 `impact` 找到并同步更新权威主题。

```powershell
# 只读；不需要网站依赖，不联网
node docs/.vitepress/scripts/knowledge.mjs search "问题或代码符号"
node docs/.vitepress/scripts/knowledge.mjs search "ONNX" --all
node docs/.vitepress/scripts/knowledge.mjs impact "UI/ColorVision.UI/PropertyEditor"
```

没有 Node.js 时直接读取已提交的 `docs/knowledge/index.md`，或用 `rg` 搜索 Markdown。本地查询和生成目录只负责定位，不证明正文与当前实现一致。

## 资料职责

| 资料 | 负责什么 |
| --- | --- |
| 根目录及局部 `AGENTS.md` | 工作规则、授权边界和检索入口 |
| 活动主题 Markdown | 当前契约、理由、源码/测试入口和验证缺口 |
| 源码、项目文件、协议与测试 | 核实实际实现和可重复行为 |
| `knowledge/index.md`、`code/`、`domains/`、`catalog.json` 和网站 | 从主题元数据生成发现、路由与展示；`knowledge/maintenance.md` 等规范正文仍由人工维护 |
| `CHANGELOG.md` 与 Git | 回溯版本变化和旧设计 |

同一事实只维护一份。简体中文是主题正文的主要维护语言；英文 `AGENTS.md`、源码旁原生 README、符号和配置键可以保留。源码旁 README 只保留包身份、随包必需的前提/风险和权威主题入口，不复制另一套完整契约。

## 常用入口

- 构建与运行：[环境与构建前提](./00-getting-started/prerequisites.md)。
- 系统责任：[系统职责与边界](./03-architecture/overview/system-overview.md)。
- 验证选择：[测试与验证](./02-developer-guide/testing.md)。
- 维护知识：[知识维护规范](./knowledge/maintenance.md)。
- 检索器规则与问题抽样：[检索验收](./knowledge/retrieval-checks.md)。

```powershell
npm run docs:knowledge
npm run docs:check
# 需要站点依赖；生成本地网页，不发布
npm run docs:build
```

字段、路径和网站检查只证明资料可发现、链接有效；语义正确性仍需核对源码、相关测试和真实问题。
