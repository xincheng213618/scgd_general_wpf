# SystemMonitor 插件

SystemMonitor 是 Windows/WPF 系统监控插件。`SystemMonitors` 单例向设置页、工具菜单和状态栏提供同一份运行时状态；插件通过 `manifest.json` 装载，发布版本取编译后 DLL 的 `FileVersion`。

完整行为、生命周期、排障和验证契约见 [SystemMonitor 知识主题](../../docs/04-api-reference/plugins/standard-plugins/system-monitor.md)。

## 包内前提与边界

- `manifest.json` 的 `id`、`dllpath` 和最低宿主要求必须与产物匹配。
- `SystemMonitors` 和 `SystemMonitorSetting` 当前位于 `ColorVision.UI.Configs` 命名空间。
- 状态栏使用 `IStatusBarProviderUpdatable`；单个性能信息源失败时应独立降级。
- 缓存清理只处理应用数据和日志相关目录，不能扩大删除范围。

## 构建与验证

```powershell
dotnet build .\Plugins\SystemMonitor\SystemMonitor.csproj -c Release -p:Platform=x64
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~SystemMonitor"
```

## 打包上传

以下命令会构建并上传插件，成功后清理本地 `.cvxp`；只有获得明确发布授权时运行。

```powershell
.\Scripts\package_plugin.bat SystemMonitor
```
