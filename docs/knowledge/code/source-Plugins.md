---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Plugins 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## Plugins/ 根目录与跨模块关联 {#module-506c7567696e73}

- [插件装配与模块知识入口](../../04-api-reference/plugins/README.md) — `plugins.index`
  从插件程序集装载、产物安装和具体模块能力定位源码；同一责任不再分开发手册与使用手册。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件项目构建、HostCopy、市场与本地安装、备份回退和提取插件；DLL目录替换、依赖补回及重启后加载的完成条件，正式打包会上传。

- [WindowsServicePlugin：选包、本机安装与恢复](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。

## Plugins/Conoscope {#module-506c7567696e732f436f6e6f73636f7065}

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 读取、内嵌 XYZ 真彩显示与原图回退、手动校正数值校验，以及版本写回和失败边界。

- [Conoscope 图像、采集与分析](../../04-api-reference/plugins/standard-plugins/conoscope.md) — `plugins.conoscope`
  Conoscope 的采集、CVCIE 首屏/XYZ 就绪、Mat 与分析快照契约；按钮成功不代表文档加载完成，联合灰尘预处理不走 Y-first。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

## Plugins/ImageProjector {#module-506c7567696e732f496d61676550726f6a6563746f72}

- [图卡生成与图片投影](../../04-api-reference/plugins/standard-plugins/pattern.md) — `plugins.pattern`
  Pattern 图卡生成、四象限线栅排列/视场、颜色与模板，及 ImageProjector 图片投影；源码同库维护但仍独立构建交付。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

## Plugins/Pattern {#module-506c7567696e732f5061747465726e}

- [图卡生成与图片投影](../../04-api-reference/plugins/standard-plugins/pattern.md) — `plugins.pattern`
  Pattern 图卡生成、四象限线栅排列/视场、颜色与模板，及 ImageProjector 图片投影；源码同库维护但仍独立构建交付。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

## Plugins/Spectrum {#module-506c7567696e732f537065637472756d}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md) — `plugins.spectrum`
  光谱仪软件 Spectrum 的连接、标定、单次测量和 CSV 导出；标定状态与测量前文件复核、EQE 输入及独立 ZIP/cvxp 发布版本来源。

- [Spectrum Socket 业务指令与完成边界](../../04-api-reference/plugins/standard-plugins/spectrum-socket.md) — `plugins.spectrum-socket`
  Spectrum Socket 的启用与状态查询、五个指令的参数和返回值；连接成功与标定就绪不同，30/60 秒取消不保证原生操作按时停止。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

## Plugins/SystemMonitor {#module-506c7567696e732f53797374656d4d6f6e69746f72}

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件项目构建、HostCopy、市场与本地安装、备份回退和提取插件；DLL目录替换、依赖补回及重启后加载的完成条件，正式打包会上传。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。

- [系统监控（SystemMonitor）](../../04-api-reference/plugins/standard-plugins/system-monitor.md) — `plugins.system-monitor`
  系统监控的 CPU/RAM 采样、手动刷新与状态栏生命周期；缓存大小包含子目录，清理只删顶层文件，逐文件失败不会单独提示。

## Plugins/WindowsServicePlugin {#module-506c7567696e732f57696e646f777353657276696365506c7567696e}

- [WindowsServicePlugin：选包、本机安装与恢复](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。

- [插件依赖与接入矩阵](../../04-api-reference/plugins/plugin-capability-matrix.md) — `plugins.capabilities`
  横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。
