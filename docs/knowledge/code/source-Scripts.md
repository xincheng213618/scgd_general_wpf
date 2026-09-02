---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Scripts 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## Scripts/ 根目录与跨模块关联 {#module-53637269707473}

- [桌面交付制品与责任路由](../../02-developer-guide/deployment/overview.md) — `delivery.deployment`
  按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。

- [客户项目与对接示例入口](../../04-api-reference/projects/README.md) — `projects.index`
  按客户业务代码、独立对接示例、旧项目归档与构建发布边界定位 Projects 的权威主题。

- [构建平台与制品边界](../../02-developer-guide/README.md) — `delivery.index`
  定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。

- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md) — `plugins.spectrum`
  Spectrum 的测量校正链、SQLite 结果和独立 ZIP 与 cvxp 双通道发布契约。

- [安装制品与运行输出](../../00-getting-started/installation.md) — `delivery.installation`
  区分完整安装制品、增量更新和源码输出，定位安装后缺依赖、配置与启动问题。

- [构建与发布脚本](../../02-developer-guide/scripts/README.md) — `delivery.scripts`
  主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

## Scripts/tests {#module-536372697074732f7465737473}

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。
