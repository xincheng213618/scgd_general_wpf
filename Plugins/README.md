# ColorVision Plugins

本目录只表示当前源码仓库里实际存在的插件项目，不再继续维护把所有历史插件或外部插件都列成“标准插件列表”的旧索引。

## 当前仓库里真实存在的插件目录

从当前源码树看，`Plugins/` 下实际可对上的项目是：

- [Plugins/Conoscope/README.md](Conoscope/README.md)：锥镜相关采集、预处理和分析插件。
- [Plugins/Spectrum/README.md](Spectrum/README.md)：光谱仪测试与色彩分析。
- [Plugins/SystemMonitor/README.md](SystemMonitor/README.md)：系统性能监控与状态栏显示。
- [Plugins/WindowsServicePlugin/README.md](WindowsServicePlugin/README.md)：Windows 服务和相关运维工具。

同时还能看到若干辅助文件：

- `Conoscope.bat`
- `Spectrum.bat`
- `SystemMonitor.bat`
- `WindowsServicePlugin.bat`
- `Directory.Build.props`

这些条目都能在当前仓库里直接找到。相反，旧索引里提到的 Pattern、ScreenRecorder、ImageProjector、YoloObjectDetection 当前都不在这个源码目录下。

## 插件当前是怎么被主程序加载的

按 [UI/ColorVision.UI/Plugins/PluginLoader.cs](../UI/ColorVision.UI/Plugins/PluginLoader.cs) 的现状，主程序启动时会：

1. 扫描 `Plugins/` 下的一级子目录。
2. 优先读取各目录下的 `manifest.json`。
3. 根据 `manifest.json` 里的插件标识和 DLL 路径更新内部缓存。
4. 如果目录里存在单个 `.deps.json`，还会先检查 `ColorVision.*` 依赖版本。
5. 最终用 `Assembly.LoadFrom(...)` 装载插件程序集。

如果目录里没有 `manifest.json`，当前实现仍会尝试按“目录名同名 DLL”的方式装载，但这只是兼容路径，不应继续写成主推荐形态。

## 当前目录结构的真实特点

这个仓库里的插件目录并不遵循单一模板。以当前几个项目为例：

- `Conoscope/` 同时包含 `Docs/`、`Analysis/`、`Layout/`、`MVS/` 等较重的业务目录。
- `Spectrum/` 包含 `Calibration/`、`Configs/`、`Data/`、`Help/`、`License/`、`Menus/`、`PropertyEditor/` 等多块功能区。
- `SystemMonitor/` 体量较轻，主要围绕 `SystemMonitors.cs`、`SystemMonitorControl.xaml(.cs)` 和状态栏提供器展开。
- `WindowsServicePlugin/` 则更偏运维工具集合，目录里既有 `ServiceManager/` 也有 `Menus/`、`CVWinSMS/`。

因此这里不再继续给出那种“每个插件都会有 App.xaml、MainWindow、Sources、Assets”的统一目录模板，因为它和当前源码并不相符。

## 现在怎么读这个目录最有效

如果你要理解插件体系，建议按下面顺序读：

1. 先看 [UI/ColorVision.UI/Plugins/PluginLoader.cs](../UI/ColorVision.UI/Plugins/PluginLoader.cs)，理解插件发现和装载模型。
2. 再看各插件自己的 `manifest.json` 和 `.csproj`，确认它到底是不是当前工作区里的真实项目。
3. 最后进入具体插件目录，例如 [Plugins/Spectrum/README.md](Spectrum/README.md) 或 [Plugins/SystemMonitor/README.md](SystemMonitor/README.md)。

## 当前已知的文档漂移

- 旧版插件索引仍然列出当前源码树里不存在的目录。
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](../docs/04-api-reference/plugins/standard-plugins/pattern.md) 已经改成现状说明，因为对应插件源码当前缺失。
- `docs/04-api-reference/plugins/standard-plugins/` 下其余几页也仍有明显的旧模板痕迹，阅读时需要继续以源码为准。

## 继续阅读

- [docs/02-developer-guide/plugin-development/overview.md](../docs/02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](../docs/04-api-reference/plugins/standard-plugins/pattern.md)
- [docs/04-api-reference/README.md](../docs/04-api-reference/README.md)

## 维护者

ColorVision 插件团队

## 许可证

所有插件遵循 ColorVision 主项目许可证。
