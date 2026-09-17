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

- [显示图案计量](../../04-api-reference/algorithms/detectors/display-metrology.md) — `algorithms.display-metrology`
  本地显示图案计量：RGB套色、九点十字RGB分离、鬼影候选、亮暗点/线缺陷/Mura、双目信号与几何、Eyebox扫描和全视场斜边SFR；公开原理与可复现合成样本，不承诺现场精度。

- [反馈归属、查询与诊断附件下载](../../02-developer-guide/backend/feedback.md) — `delivery.backend-feedback`
  反馈提交按服务端账号归属，普通用户只读本人记录，研发只读账号/API key可下载全部诊断附件，管理员独立更新状态；新目录使用北京时间和机器标识。

- [构建平台与制品边界](../../02-developer-guide/README.md) — `delivery.index`
  定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件项目构建、HostCopy、市场与本地安装、备份回退和提取插件；DLL目录替换、依赖补回及重启后加载的完成条件，正式打包会上传。

- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md) — `plugins.spectrum`
  光谱仪软件 Spectrum 的连接、标定、单次测量和 CSV 导出；标定状态与测量前文件复核、EQE 输入及独立 ZIP/cvxp 发布版本来源。

- [代码行数与 Git 历史统计](../../02-developer-guide/scripts/code-statistics.md) — `delivery.code-statistics`
  统计工作区代码行数与 Git 提交历史，说明文件筛选、变更量口径、缓存和图表生成依赖；历史快照不包含未提交修改，HTML 构建依赖外部构建器。

- [安装制品与运行输出](../../00-getting-started/installation.md) — `delivery.installation`
  区分完整安装制品、增量更新和源码输出，定位安装后缺依赖、配置与启动问题。

- [构建与发布脚本](../../02-developer-guide/scripts/README.md) — `delivery.scripts`
  主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [ProjectARVRPro.IntegrationDemo](../../04-api-reference/projects/project-arvr-pro-integration-demo.md) — `projects.arvr-pro-demo`
  独立 net48 ARVRPro TCP/JSON Demo 的公开字段、ACK 与最终完成判据、切图自动确认、逐条消息超时及 JSON/CSV 导出；正常退出不代表最终 SN 和明确 PASS 已核验。

- [ProjectARVRPro SemiAuto](../../04-api-reference/projects/project-arvr-pro-semi-auto.md) — `projects.arvr-pro-semi-auto`
  独立 ProjectARVRPro SemiAuto 软件的 ARVR/GECS 双 Socket、可配置指令映射、PG 成功门禁、四页签操作界面、结果解析及客户 ZIP 验证边界。

## Scripts/tests {#module-536372697074732f7465737473}

- [测试与验证](../../02-developer-guide/testing.md) — `delivery.testing`
  按改动范围选择managed、native、脚本、后端和知识验证，不以局部通过代表完整验收。
