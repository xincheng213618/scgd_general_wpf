---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 构建、测试与交付

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

克隆环境、构建依赖、测试、发布脚本和更新。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [桌面交付制品与责任路由](../../02-developer-guide/deployment/overview.md) — `delivery.deployment`
  按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。

- [安装、构建与运行入口](../../00-getting-started/README.md) — `delivery.start`
  克隆代码后的源码问答、本地构建、安装和运行分流；只问Codex不需要先启动程序。

- [Android 运维伴侣](../../02-developer-guide/backend/android-operations.md) — `delivery.android-operations`
  Android原生运维入口、现场HTTPS与固定签名中继的职责边界；连接、可见证据和操作授权不能互相替代。

- [插件市场后端](../../02-developer-guide/backend/README.md) — `delivery.backend`
  插件市场Flask后端的文件制品、索引、认证与API边界，以及隔离存储的启动测试。

- [构建平台与制品边界](../../02-developer-guide/README.md) — `delivery.index`
  定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。

- [自动更新](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  主程序及插件更新、检查结果一次性消费、失败元数据回退、目录替换与启动恢复的实现和验收边界。

- [安装制品与运行输出](../../00-getting-started/installation.md) — `delivery.installation`
  区分完整安装制品、增量更新和源码输出，定位安装后缺依赖、配置与启动问题。

- [系统要求](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  首次构建所需Windows x64、.NET与C++工具链，区分已有native DLL与干净克隆。

- [构建与发布脚本](../../02-developer-guide/scripts/README.md) — `delivery.scripts`
  主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。
