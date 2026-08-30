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

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

- [Conoscope 插件](../../04-api-reference/plugins/standard-plugins/conoscope.md) — `plugins.conoscope`
  Conoscope 的图像观察、VAM 分析、原生依赖、单插件构建与授权发布入口。

- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md) — `plugins.spectrum`
  Spectrum 的测量校正链、SQLite 结果和独立 ZIP 与 cvxp 双通道发布契约。

- [SystemMonitor 插件](../../04-api-reference/plugins/standard-plugins/system-monitor.md) — `plugins.system-monitor`
  SystemMonitor 的性能采样、状态栏、窗口生命周期与停止采样边界。

- [WindowsServicePlugin](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin 的服务安装、配置和管理员权限边界；不能把诊断请求当作系统修改授权。
