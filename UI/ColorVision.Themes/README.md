# ColorVision.Themes

Windows WPF 主题资源与窗口外观支持包，附带基础控件和转换器。目标框架、依赖和包版本以 [ColorVision.Themes.csproj](./ColorVision.Themes.csproj) 为准。

主题选择、资源注入、系统跟随、标题栏生命周期及配置边界的单一说明是 [主题选择、资源应用与窗口外观](../../docs/04-api-reference/ui-components/ColorVision.Themes.md)（`ui.themes`）。此相对链接面向源码仓库；从 NuGet 阅读时，请按包版本在项目源码中查看对应文档，不把当前分支契约直接套用到旧包。

## 包接入

使用 `ColorVision.Themes` 命名空间。已有宿主初始化资源时，通过 `Application.ApplyTheme` 选择主题，在窗口首次 Loaded 前调用一次 `Window.ApplyCaption` 接入标题栏；二者的选择/实际状态及失败语义见权威主题。

空白 WPF 宿主可以在 UI 线程启动阶段先建立资源，再选择跟随系统：

```csharp
using ColorVision.Themes;
using System.Windows;

// 一次性初始化；不要放进反复刷新的事件处理器。
Application.Current.ForceApplyTheme(Theme.Light);
Application.Current.ApplyTheme(Theme.UseSystem);
```

Themes 包本身不保存主题配置。`ThemeConfig` / `ThemePropertiesEditor` 是 ColorVision.UI 中的集成，不是只引用此包就具备的自动持久化能力。

## 本地构建

从仓库根目录在 Windows PowerShell 执行；需要对应 SDK/WPF 构建环境，依赖未还原时可能联网。该命令写入本地产物，并按项目配置生成包，不安装、不上传或切换系统主题。

```powershell
dotnet build .\UI\ColorVision.Themes\ColorVision.Themes.csproj -p:Platform=x64
```

发布请使用仓库的 [UI DLL 发布契约](../../docs/04-api-reference/ui-components/publishing.md)，不要把本地包生成当成已发布。
