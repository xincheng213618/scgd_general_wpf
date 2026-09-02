# SystemMonitor 插件

Windows/x64 WPF 系统监控插件，提供“系统监控”菜单/设置页和可选状态栏项。目标框架、宿主依赖以 `SystemMonitor.csproj` 与 `Plugins/Directory.Build.props` 为准；manifest 标识和最低宿主要求须与产物匹配，发布版本取编译后 DLL 的 `FileVersion`。

包使用前提：

- 性能计数器可能不可用；0或空值不证明资源空闲。磁盘、网卡和性能计数器没有统一的逐项失败隔离。
- “清理缓存”会直接删除应用数据目录与日志父目录的顶层文件，没有事前确认或缓存文件白名单；成功计数不代表全部删除。操作前确认目标、保留文件和删除授权。
- 关闭窗口不保证停止状态栏所需的采样。状态栏显示与采样条件由插件配置共同决定。

[SystemMonitor 权威主题](../../docs/04-api-reference/plugins/standard-plugins/system-monitor.md)维护入口、配置、统计/清理范围、生命周期、排障和构建发布命令。发布会上传 `.cvxp`，需要明确发布授权。

本 README 随插件输出复制；`docs/` 不随本文件自动交付。相对链接仅用于匹配版本的完整源码仓库，包使用者需回到同版本仓库读取完整契约。
