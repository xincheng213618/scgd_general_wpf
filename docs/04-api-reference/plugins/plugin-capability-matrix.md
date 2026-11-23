---
knowledge_id: "plugins.capabilities"
knowledge_type: "reference"
status: "current"
summary: "横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。"
aliases: ["插件依赖","插件扩展点","插件权限","哪些插件会操作设备或服务","插件状态栏比较"]
code_paths: ["Plugins/Conoscope/","Plugins/Spectrum/","Plugins/SystemMonitor/","Plugins/WindowsServicePlugin/","Plugins/Pattern/"]
related: ["plugins.index","plugins.model","plugins.getting-started","plugins.conoscope","plugins.spectrum","plugins.system-monitor","plugins.windows-service","plugins.pattern"]
---

# 插件依赖与接入矩阵

本页汇总通用插件的入口和主要外部影响。客户判定、MES 及专用流程由项目包承担。 [项目知识入口](../projects/README.md)

## 当前源码插件总表

| 插件 | 界面或运行入口 | 外部影响 |
| --- | --- | --- |
| [Pattern](./standard-plugins/pattern.md) | 工具 → 图卡生成工具 / 图片投影工具 | 导出、清理和全屏投影会影响文件或显示器 |
| [Conoscope](./standard-plugins/conoscope.md) | 工具 → VAM，或符合条件的 ImageEditor 右键 | 分别核对本地 CVCIE、Engine 测量采集与 MVS 观察相机 |
| [Spectrum](./standard-plugins/spectrum.md) | 光谱窗口、Socket 和调度 | 测量与校零会操作设备，结果保存到 SQLite |
| [SystemMonitor](./standard-plugins/system-monitor.md) | 工具 → 系统监控，同名设置页和可选状态栏项 | 依赖系统计数器，执行清理会删除文件 |
| [WindowsServicePlugin](./standard-plugins/windows-service.md) | 服务管理器和安装向导 | 安装或恢复可修改文件、数据库、服务与进程 |

已退役的 CameraTest 参见[相机生产调试归档](./standard-plugins/camera-test.md)。

实际操作所需的应用角色、Windows 权限和设备条件见对应插件专题。

## 按宿主扩展点定位

扩展身份从源码中的 `MenuItemBase`、`IMenuItemProvider`、`IStatusBarProvider`、`IConfigSettingProvider` 和 `IIEditorToolContextMenu` 查找。配置持久化与设置页注册是两条入口，宿主和窗口的 provider 也应分别核对。 [插件装载与发现](../../02-developer-guide/plugin-development/overview.md)

Spectrum 的 Socket 和调度复用测量 Manager，可在相应服务和设备就绪后独立于窗口使用。 [Spectrum Socket](./standard-plugins/spectrum-socket.md)

## 构建与交付差异

版本、manifest 身份、共享依赖剥离、安装和回退的共用规则见[插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md)。发布版本来自编译产物；manifest 是发布时同步的元数据，不单独证明版本或 ABI 一致。

| 插件 | 本地复制与交付差异 | 命令及资源清单 |
| --- | --- | --- |
| Pattern | HostCopy 默认关闭，需启用 `EnablePatternHostCopy` 并提供有效 `SolutionDir`；图卡与投影统一编译、复制和发布。完整独立运行输出与剥离共享依赖的 `.cvxp` 用途不同 | [构建、独立运行与交付](./standard-plugins/pattern.md#构建、独立运行与交付) |
| Conoscope | 有效 `SolutionDir` 下，通用 HostCopy 写两套宿主插件目录；额外 target 还向 Debug/Release 宿主根目录复制顶层项目引用 DLL 及存在的 PDB | [本地构建、宿主复制与发布](./standard-plugins/conoscope.md#本地构建、宿主复制与发布) |
| SystemMonitor / WindowsServicePlugin | 使用通用 HostCopy 条件和 Debug/Release 双目录复制；WindowsServicePlugin 的插件 `.cvxp` 与业务服务 ZIP 分别发布 | [SystemMonitor 构建](./standard-plugins/system-monitor.md#本地构建与测试)、[服务插件交付](./standard-plugins/windows-service.md#构建、发布与验证) |
| Spectrum | 没有项目 HostCopy；正式发布同时维护独立 ZIP 和插件 `.cvxp` 两个更新源，使用专用脚本 | [本地构建](./standard-plugins/spectrum.md#本地构建与测试)、[双通道发布](./standard-plugins/spectrum.md#双通道发布-需明确发布授权) |

本地构建会写输出，也可能触发上述宿主复制；发布命令还会上传。运行对应发布命令前需明确发布对象和授权。文件齐全、复制成功或发布成功都不等于新进程已正确装载，运行发现和依赖诊断见[插件装载契约](../../02-developer-guide/plugin-development/overview.md)。

## 按问题进入验证主题

| 问题 | 对应检查与完成条件 |
| --- | --- |
| 图卡像素、颜色、模板或投影不符合预期 | [Pattern 图卡与配置](./standard-plugins/pattern.md#图卡入口与生成)、[图片投影](./standard-plugins/pattern.md#图片投影)及[验证范围](./standard-plugins/pattern.md#验证范围)；区分生成像素、多屏 DPI 和实际光学效果 |
| Conoscope 采集后没有图，或分析点不匹配 | [采集完成](./standard-plugins/conoscope.md#采集完成、文件发现与打开不是一个信号)、[关注点与分析](./standard-plugins/conoscope.md#关注点模板与分析快照)；文件出现、通道就绪与分析对齐各有判据 |
| Spectrum 已连接但不能测量，或远程结果不完整 | [设备与标定](./standard-plugins/spectrum.md#设备、标定和测量)、[模块验收](./standard-plugins/spectrum.md#验收)、[Socket 指令](./standard-plugins/spectrum-socket.md)；区分连接、标定 readiness、测量、落库与协议响应 |
| 系统监控为空，关闭窗口后仍采样，或清理未归零 | [监控排障](./standard-plugins/system-monitor.md#异常与排查)和[验证范围](./standard-plugins/system-monitor.md#验证范围)；检查数据源、状态栏配置和实际清理范围 |
| 服务安装、数据库迁移或恢复部分失败 | [安装顺序](./standard-plugins/windows-service.md#安装、数据库与配置顺序)和[恢复限制](./standard-plugins/windows-service.md#备份和恢复不等于自动回滚)；按文件、配置、数据库、服务逐层确认，不能以安装方法返回代替整体成功 |

真机采集、校零、投影、缓存删除及服务或数据库变更需在获准环境验证。
