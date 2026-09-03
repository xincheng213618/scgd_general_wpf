---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 仓库与知识基础设施 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## 仓库根文件 {#module-2e}

- [安装、构建与运行入口](../../00-getting-started/README.md) — `delivery.start`
  克隆代码后的源码问答、本地构建、安装和运行分流；只问Codex不需要先启动程序。

- [ColorVision 项目知识入口](../../index.md) — `governance.home`
  ColorVision AI优先知识入口：按问题定位能力、代码、测试与维护约束。

- [仓库知识使用约定](../../README.md) — `governance.knowledge`
  说明仓库知识入口、按需检索、源码核对和文档与代码同步维护的共同规则。

- [插件装配与模块知识入口](../../04-api-reference/plugins/README.md) — `plugins.index`
  按程序集装载、产物交付、插件能力比较和模块操作定位权威主题与源码。

- [客户项目与对接示例入口](../../04-api-reference/projects/README.md) — `projects.index`
  按客户业务代码、独立对接示例、旧项目归档与构建发布边界定位 Projects 的权威主题。

- [构建平台与制品边界](../../02-developer-guide/README.md) — `delivery.index`
  定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。

- [ColorVision.Engine 工程、资源与依赖](../../04-api-reference/engine-components/ColorVision.Engine.md) — `engine.host`
  ColorVision.Engine工程的条件引用、NuGet/DLL依赖回退与资源打包；schema嵌入程序集，缺少输出散文件不等于漏包，也不保证脱离UI源码独立构建。

- [ColorVisionDriver：实验性内核驱动骨架](../../03-architecture/components/kernel-driver.md) — `platform.kernel-driver`
  ColorVisionDriver 实验性 WDM 驱动骨架的两个 IOCTL、WDK 构建输入与接入边界；尚未接入主程序、服务宿主或正式发布链。

- [Conoscope 图像、采集与分析](../../04-api-reference/plugins/standard-plugins/conoscope.md) — `plugins.conoscope`
  Conoscope 的采集、CVCIE 首屏/XYZ 就绪、Mat 与分析快照契约；按钮成功不代表文档加载完成，联合灰尘预处理不走 Y-first。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件项目构建、HostCopy、市场与本地安装、备份回退和提取插件；DLL目录替换、依赖补回及重启后加载的完成条件，正式打包会上传。

- [图卡生成与图片投影](../../04-api-reference/plugins/standard-plugins/pattern.md) — `plugins.pattern`
  Pattern 图卡生成、四象限线栅排列/视场、颜色与模板，及 ImageProjector 图片投影；源码同库维护但仍独立构建交付。

- [代码行数与 Git 历史统计](../../02-developer-guide/scripts/code-statistics.md) — `delivery.code-statistics`
  统计工作区代码行数与 Git 提交历史，说明文件筛选、变更量口径、缓存和图表生成依赖；历史快照不包含未提交修改，HTML 构建依赖外部构建器。

- [原生 helper 测试与调试](../../02-developer-guide/engine-development/native-testing.md) — `delivery.native-testing`
  opencv\_helper\_test 的实际入口、工具集与配置映射、专项参数、DLL/样本前提和退出码边界；默认运行与真实样本验收不同。

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [构建与发布脚本](../../02-developer-guide/scripts/README.md) — `delivery.scripts`
  主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [知识维护规范](../maintenance.md) — `governance.maintenance`
  定义AI与人共同维护知识的字段、事实责任、源码反向影响检查和验收流程。

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。

- [软件许可协议](../../05-resources/legal/software-agreement.md) — `platform.license`
  保留软件许可协议原文供定位，不由AI重新解释或改写许可条款。

## .github/workflows {#module-2e6769746875622f776f726b666c6f7773}

- [构建平台与制品边界](../../02-developer-guide/README.md) — `delivery.index`
  定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。

- [构建与发布脚本](../../02-developer-guide/scripts/README.md) — `delivery.scripts`
  主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。

## docs {#module-646f6373}

- [仓库知识使用约定](../../README.md) — `governance.knowledge`
  说明仓库知识入口、按需检索、源码核对和文档与代码同步维护的共同规则。

- [知识维护规范](../maintenance.md) — `governance.maintenance`
  定义AI与人共同维护知识的字段、事实责任、源码反向影响检查和验收流程。

## docs/.vitepress {#module-646f63732f2e766974657072657373}

- [仓库知识使用约定](../../README.md) — `governance.knowledge`
  说明仓库知识入口、按需检索、源码核对和文档与代码同步维护的共同规则。

- [知识维护规范](../maintenance.md) — `governance.maintenance`
  定义AI与人共同维护知识的字段、事实责任、源码反向影响检查和验收流程。

- [知识检索与问答验收](../retrieval-checks.md) — `governance.retrieval`
  用固定检索问题和无个人记忆的源码问答抽样，验证知识入口能否定位正确模块、边界和测试。

## docs/knowledge {#module-646f63732f6b6e6f776c65646765}

- [知识检索与问答验收](../retrieval-checks.md) — `governance.retrieval`
  用固定检索问题和无个人记忆的源码问答抽样，验证知识入口能否定位正确模块、边界和测试。
