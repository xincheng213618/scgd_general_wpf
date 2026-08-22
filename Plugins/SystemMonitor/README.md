# SystemMonitor 插件

SystemMonitor 是轻量系统监控插件。`SystemMonitors` 单例负责采集和刷新数据，设置页、工具菜单和状态栏使用同一份运行时状态。发布版本来自 `SystemMonitor.csproj` 编译出的 DLL `FileVersion`；最低宿主要求读取 `manifest.json`。

## 当前能力

| 能力 | 入口 | 说明 |
| --- | --- | --- |
| 监控单例 | `SystemMonitors.cs` | 刷新性能、磁盘、网络、进程、运行时、GPU 和缓存数据 |
| 设置页与工具菜单 | `SystemMonitorControl.xaml(.cs)` | 展示单例数据并提供刷新、清理入口 |
| 状态栏 | `SystemMonitorIStatusBarProvider.cs` | 按配置动态投影时间、运行时长、CPU、RAM 和磁盘项 |

配置包括 `UpdateSpeed`、时间格式以及时间、CPU、RAM、运行时长和磁盘的状态栏开关。定时刷新间隔不得小于 100 ms；性能计数器或单个系统信息源失败时应降级，不应阻止插件窗口打开。

## 排查

| 现象 | 第一检查点 |
| --- | --- |
| Tool 菜单无入口 | 插件目录、`manifest.json`、`SystemMonitor.dll`、菜单 Provider |
| 状态栏不刷新 | Provider 是否监听配置变化，`StatusBarItemsChanged` 是否触发 |
| CPU/RAM 为空 | Windows 性能计数器是否初始化失败 |
| 磁盘列表为空 | drive 是否 ready、权限是否可读 |
| 网络信息缺失 | 是否被 loopback/tunnel 过滤，是否存在 IPv4 地址 |
| 清理缓存失败 | 目标目录权限和文件占用；不要扩大清理范围 |

## 构建与验证

```powershell
dotnet build .\Plugins\SystemMonitor\SystemMonitor.csproj -c Release -p:Platform=x64
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~SystemMonitor"
```

构建后验证设置页、工具菜单、状态栏动态增删、磁盘/网络/进程刷新和缓存清理的权限失败路径。打包和发布使用：

```powershell
.\Scripts\package_plugin.bat SystemMonitor
```

## 维护边界

- `SystemMonitors` 和 `SystemMonitorSetting` 当前位于 `ColorVision.UI.Configs` 命名空间。
- 状态栏依赖 `IStatusBarProviderUpdatable`，不是静态 Provider。
- 监控失败项应独立降级，不能拖垮插件或主程序。
- 清理命令只处理应用数据和日志相关目录。
