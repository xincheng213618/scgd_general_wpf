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

这里是 ColorVision 的版本化项目知识，不是必须从头阅读的手册。AI 先查任务入口，再按需读取主题、源码和测试；网页只是同一份知识的展示视图。

## 从问题到实现

1. 从简短的[知识地图](./knowledge/index.md)选择实际源码根和模块，再找主题；不知道代码归属时可直接使用本地查询或次级能力目录，不必读完全部索引。
2. 阅读主题的范围、状态、关键约束和源码入口；跨模块时再读 `related`。
3. 核对当前分支的实现与测试。文档记录契约和理由，源码说明实际实现；两者矛盾时报告并调查，不凭摘要改代码。
4. 修改后查反向影响，更新受影响的知识和验证入口；不要重新生成一份无人核验的全仓说明。

```powershell
# 只读查询：不需要 npm install，不会调用模型或联网
node docs/.vitepress/scripts/knowledge.mjs search "属性编辑器"
node docs/.vitepress/scripts/knowledge.mjs search "ConfigHandler.ReloadFromDisk读取失败"
node docs/.vitepress/scripts/knowledge.mjs search "ONNX" --all
node docs/.vitepress/scripts/knowledge.mjs impact "UI/ColorVision.UI/PropertyEditor"
```

没有 Node.js 时，直接读取随仓库提交的 `docs/knowledge/index.md`，或用 `rg` 搜索 Markdown；网站和查询工具都不是理解项目的前置条件。

本地 `search` 是元数据词法检索：使用标题、摘要、别名、正文地址及源码/测试路径，不扫描整份正文或全仓代码，也不调用向量服务。完整限定符优先；`Namespace.Type.Member` 没有完整命中时，可回退到 `Namespace.Type` / `Type`，不把通用成员名 `Save` 等当成所属模块。源码路径支持 PowerShell 常见的反斜杠，并可匹配具体尾部路径或文件名。

回退中优先较完整的所属类型限定名或较具体的尾部路径；具体度相同时，标题、摘要或别名明确描述的类型优先于仅在关联源码路径中出现的类型。关联路径也可能来自调用方，因此这些规则仍只是定位线索，不是自动识别事实所有者的调用图。

搜索结果只列主题、摘要和匹配方式（`exact`、`qualified-symbol`、`owner-fallback`、`text`），不逐项展开全部源码路径；选中主题后再读其元数据。`impact` 仍列出实际相交路径，用于代码变更后的反向核对。

这种回退只提供核对入口，不证明某个成员确实存在、行为已支持或主题涵盖全部调用链。没有命中时，先用较短类型名、能力词或 `rg` 查正文/源码；不要把检索无结果解释为功能不存在。网页搜索还收录正文片段，与本地命令不是同一排序器，但两者都引用同一份主题知识。

## 知识的职责

| 资料 | 负责什么 | 不代表什么 |
| --- | --- | --- |
| 根目录及局部 `AGENTS.md` | 工作规则、边界和检索入口 | 不是全部项目知识，也不授予发布或设备操作权限 |
| 主题 Markdown 正文与元数据 | 业务含义、行为契约、设计原因、实现和测试定位 | `current` 不等于本机已验证全部行为 |
| 当前源码、项目文件、协议样例和测试 | 核实实际实现与可重复验证 | 测试存在或通过，不代表真机/交付链已验证 |
| 生成的知识目录、JSON 和网页 | 发现、路由和展示 | 不独立维护另一套事实，不替代原始正文 |
| CHANGELOG 与 Git 历史 | 回溯变化和旧设计 | 不能覆盖当前主题中的行为定义 |

根 `README.md` 负责项目介绍与首个检索入口，`CONTRIBUTING.md` 保留贡献约定；版本变化读取当前检出的根 `CHANGELOG.md`，不要把远端另一个分支的版本说明当作本地事实。许可查看根 `LICENSE.md` 及[软件许可协议原文](./05-resources/legal/software-agreement.md)，知识目录不复制或改写条款。

## 按任务进入

- 不知道代码归属：从[生成的源码地图](./knowledge/index.md)定位；需要判断跨模块关系时读[系统职责与边界](./03-architecture/overview/system-overview.md)。
- 需要构建或运行：看[环境与首次构建](./00-getting-started/prerequisites.md)，区分代码问答、构建和连接设备；只读问答不要求先完成环境准备。
- 修改代码：通过[知识地图](./knowledge/index.md)定位主题，再看[验证入口](./02-developer-guide/testing.md)。
- 修改知识本身：遵守[维护规范](./knowledge/maintenance.md)，使用[检索验收问题](./knowledge/retrieval-checks.md)。

## 单一正文与派生网站

主题正文以简体中文为主，同一事实只维护一份。`AGENTS.md` 保留英文；源码附近原生英文 README、包说明也可以保留，不做全仓翻译。类型、方法、字段、错误码保留原始拼写并补充必要别名。完整英文镜像不是 AI 检索的前提；不同语言提问的质量需要单独抽样，不能假定翻译等于行为一致。

按实际代码职责组织知识：主程序、UI、Engine、Native、插件、客户项目、Web 和构建脚本是定位起点；跨模块执行链保留独立主题。操作含义、实现契约和故障验证不是分别给不同人群维护的三套正文。源码附近 README 仍可保存模块特有说明；主题引用它们，不复制第二份契约。

知识地图和网站导航从 `code_paths` 派生源码关联，能力领域目录保留为辅助检索。宽泛目录关联只表示需要复核，不代表调用图已完整覆盖。旧的 `00-*` 至 `05-*` 路径保留稳定链接价值，但不再决定知识归属；纯目录或重复手册合并后退出检索，必要时只保留旧 URL 跳转。

```powershell
npm run docs:knowledge
npm run docs:check
# 以下需要 Node.js 22+ 和 npm ci 安装站点依赖，生成本地网页但不发布
npm run docs:build
```

字段和路径检查只证明知识可发现、引用有效；语义正确性由源码核对、相应测试和检索抽样共同验证。
