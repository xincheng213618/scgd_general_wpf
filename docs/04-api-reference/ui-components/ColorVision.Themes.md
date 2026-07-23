# ColorVision.Themes

`UI/ColorVision.Themes/` 是 WPF 主题资源和窗口外观支持库。它负责固定主题切换、资源字典注入、系统主题跟随、标题栏/图标联动和少量通用控件；不是任意自定义主题平台。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 应用启动后主题没生效 | `Application.ApplyTheme` 调用时机、`ThemeManager.CurrentTheme` |
| 某个主题切换时报资源找不到 | `Themes/*.xaml`、`.csproj` 资源打包配置 |
| 标题栏颜色不跟随主题 | 是否调用 `Window.ApplyCaption` 或使用 `BaseWindow` |
| `UseSystem` 不跟随 Windows | `AppsUseLightTheme`、`SystemUsesLightTheme`、系统事件监听 |
| 图标或上传背景缺失 | `Assets/Image/`、`Assets/uploadbg.avif` 是否进入包/输出目录 |
| 自定义主题接不上 | 当前没有 `Theme.Custom` 或运行时主题注册模型 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 主题枚举 | `Theme` | 仅有 `UseSystem`、`Light`、`Dark` |
| 主题切换 | `ThemeManager` | 维护 `CurrentTheme`、`CurrentUITheme`，装载资源字典并触发变更事件 |
| 应用入口 | `ThemeManagerExtensions` | `Application.ApplyTheme`、`Application.ForceApplyTheme` |
| 窗口外观 | `Window.ApplyCaption`、`BaseWindow` | 更新 DWM 标题栏颜色和主题图标 |
| 主题资源 | `Themes/Base.xaml`、`Dark.xaml`、`White.xaml` | 固定资源字典集合 |
| 附带控件 | `Controls/`、`Converter/`、`Utilities/` | 主题库内复用控件、转换器和工具 |

## 主题链路

1. 上层 UI 或配置选择主题。
2. `Application.ApplyTheme` 调到 `ThemeManager.Current.ApplyTheme`。
3. 如果选择 `UseSystem`，先按 Windows 应用主题解析为浅色或深色。
4. `ThemeManager` 把基础资源和目标主题资源加入 `Application.Resources.MergedDictionaries`。
5. 更新 `CurrentTheme` / `CurrentUITheme` 并触发主题变更事件。
6. 调过 `ApplyCaption` 的窗口同步更新标题栏颜色和图标。

## 系统主题跟随

`UseSystem` 依赖 Windows 事件和注册表值，不是框架层的实时同步服务。

| 项 | 当前实现 |
| --- | --- |
| 监听事件 | `SystemEvents.UserPreferenceChanged`、`SystemParameters.StaticPropertyChanged` |
| 应用主题判断 | `Personalize\\AppsUseLightTheme` |
| 系统主题判断 | `Personalize\\SystemUsesLightTheme` |
| 标题栏 | 深色启用沉浸式暗色；浅色回到系统默认 |

## 边界

- 主题持久化、预览卡编辑器、菜单和快捷键入口在 `UI/ColorVision.UI/Themes`，不是本项目单独完成。
- `ThemesHotKey` 负责主题菜单、`Ctrl + Shift + T` 轮换和写入 `ThemeConfig.Instance.Theme`。
- 旧文档提到的 `Theme.Custom`、`ThemeManager.ResourceDictionaryCustom`、`ThemeConfig.FollowSystem` 当前不存在。
- 新增主题不是只加一个 XAML，还要改枚举、资源列表、标题栏逻辑、图标策略和包资源。

## 新增或修改主题验收

| 验收项 | 要查什么 |
| --- | --- |
| 目标框架 | `ColorVision.Themes.csproj` 的 `net8.0-windows7.0;net10.0-windows7.0` |
| 包元数据 | `GeneratePackageOnBuild`、`PackageReadmeFile`、`README.md` |
| 第三方依赖 | `HandyControl` 版本和宿主输出目录依赖一致 |
| 资源字典 | 每个内置主题切换时都能加载对应 XAML |
| 运行时切换 | 启动后和运行时调用 `ApplyTheme` / `ForceApplyTheme` 都能更新全局资源 |
| 窗口外观 | 深色、浅色标题栏和窗口图标符合当前实现 |
| 系统跟随 | Windows 主题变化后，`UseSystem` 能按注册表和系统事件更新 |
| 包内资源 | `ColorVision.ico`、`ColorVision1.ico`、`uploadbg.avif` 不丢失 |

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 理解主题切换 | `ThemeManager.cs`、`ThemeManagerExtensions.cs`、`Theme.cs` |
| 理解菜单和配置接入 | `UI/ColorVision.UI/Themes/ThemeConfig.cs`、`ThemesHotKey.cs` |
| 检查主题资源 | `Themes/Base.xaml`、`Themes/Dark.xaml`、`Themes/White.xaml` |
| 判断能否扩展主题 | 先查 `Theme` 枚举和 `ThemeManager`，当前没有开放注册模型 |
