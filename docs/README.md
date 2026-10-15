---
knowledge_id: "governance.knowledge"
knowledge_type: "index"
status: "current"
summary: "说明 AGENTS.md 的按需知识扩展、权威主题与生成资料各自的职责和入口。"
aliases: ["文档怎么用", "AI共治", "AI知识库", "AGENTS.md", "documentation", "版本历史入口", "CHANGELOG.md"]
code_paths: ["README.md", "AGENTS.md", "CHANGELOG.md", "docs/AGENTS.md", "docs/.vitepress/config.mts", "docs/.vitepress/scripts"]
test_paths: ["docs/.vitepress/scripts/knowledge.test.mjs"]
related: ["governance.maintenance", "governance.retrieval", "platform.system", "platform.license"]
---

# 仓库知识使用约定

`docs/` 是 `AGENTS.md` 的按需知识扩展。AI 从根目录和最近的 `AGENTS.md` 获取工作规则，再读取任务所需的主题、源码和测试。网页展示同一份资料，不另设人类阅读路线。

## 从问题到实现

已知归属时直接读取主题；归属不清时用[知识地图](./knowledge/index.md)或本地查询。查询只需 Node，不联网、不安装网站依赖：

```powershell
node docs/.vitepress/scripts/knowledge.mjs search "问题或代码符号" --limit 5
node docs/.vitepress/scripts/knowledge.mjs search "ONNX" --all --limit 5
```

默认查当前主题；无 Node 时读已提交地图或用 `rg`。自然问句未命中时，改用真实界面名、符号或路径。结构化读取见 [JSON 查询结果](./knowledge/retrieval-checks.md#json-查询结果)。选择主题后核对状态及当前实现，按根 `AGENTS.md` 决定是否同步知识；查询结果只负责定位。

## 资料职责

| 资料 | 负责什么 |
| --- | --- |
| 根目录及局部 `AGENTS.md` | 工作规则、授权边界和按需入口 |
| 主题 Markdown | 能力契约、理由、前提、源码/测试入口和验证缺口 |
| 源码、项目文件、协议与测试 | 核实实现及可重复行为；与契约冲突时需要调查 |
| 生成地图、`catalog.json`、导航和网站 | 从元数据定位和展示主题；源码地图保留完整关联，侧栏只给紧凑入口 |
| 源码旁 README | 模块/包入口及随包必需的前提与风险，链接权威主题 |
| `CHANGELOG.md`、Git、`docs/_history/` | 版本变化与历史；不作为当前能力入口 |

`docs/_history/` 保存截至 `1.4.14.37` 的旧版主程序发布记录等内部资料，不进入目录、搜索、导航或网站构建，需回溯时直接从仓库读取。

## 常用入口

- 构建：[环境与构建前提](./00-getting-started/prerequisites.md)；验证：[测试与验证](./02-developer-guide/testing.md)。
- 架构：[系统职责与边界](./03-architecture/overview/system-overview.md)。
- 编辑知识：[维护规范](./knowledge/maintenance.md#按变更选择验证)；维护检索工具或做问答抽样：[检索验收](./knowledge/retrieval-checks.md)。普通代码任务无需通读这两份规范。
