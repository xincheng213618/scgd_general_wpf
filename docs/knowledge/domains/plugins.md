---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 插件与扩展

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

插件发现、生命周期、已有插件和集成边界。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [插件装配与模块知识入口](../../04-api-reference/plugins/README.md) — `plugins.index`
  从插件程序集装载、产物安装和具体模块能力定位源码；同一责任不再分开发手册与使用手册。

- [Conoscope 图像、采集与分析](../../04-api-reference/plugins/standard-plugins/conoscope.md) — `plugins.conoscope`
  Conoscope 的采集、CVCIE 首屏/XYZ 就绪、Mat 与分析快照契约；按钮成功不代表文档加载完成，联合灰尘预处理不走 Y-first。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [PluginKit SDK 打包器](../../02-developer-guide/plugin-development/sdk-packaging.md) — `plugins.sdk-packaging`
  独立 PluginKit 的项目命名、CLI/config 参数、构建与发布模式、包内容和失败排查；显式 config 的上传行为与无参数运行不同。

- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md) — `plugins.spectrum`
  光谱仪软件 Spectrum 的连接、标定、单次测量和 CSV 导出；标定状态与测量前文件复核、EQE 输入及独立 ZIP/cvxp 发布版本来源。

- [Spectrum Socket 业务指令与完成边界](../../04-api-reference/plugins/standard-plugins/spectrum-socket.md) — `plugins.spectrum-socket`
  Spectrum Socket 的启用与状态查询、五个指令的参数和返回值；连接成功与标定就绪不同，30/60 秒取消不保证原生操作按时停止。

- [WindowsServicePlugin：选包、本机安装与恢复](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

- [系统监控（SystemMonitor）](../../04-api-reference/plugins/standard-plugins/system-monitor.md) — `plugins.system-monitor`
  系统监控的 CPU/RAM 采样、手动刷新与状态栏生命周期；缓存大小包含子目录，清理只删顶层文件，逐文件失败不会单独提示。
