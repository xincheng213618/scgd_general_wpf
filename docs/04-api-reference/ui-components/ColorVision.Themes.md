# ColorVision.Themes

本页只描述 UI/ColorVision.Themes 当前已经落地的主题能力，不再延续旧文档里那种“主题开发框架 + 自定义主题平台 + 完整 FAQ 教程”的写法。

## 模块定位

ColorVision.Themes 当前更接近一个 WPF 主题资源与窗口外观支持库，核心职责主要有四类：

- 定义 Theme 枚举和主题切换入口
- 向 Application 注入资源字典
- 跟随 Windows 主题变化更新界面
- 处理窗口标题栏颜色和图标联动

它不是一个已经抽象完成的“任意自定义主题平台”。旧文档里提到的 Theme.Custom、ResourceDictionaryCustom、完整自定义主题注册流程，在当前代码中都没有对应实现。

## 当前最关键的文件

从当前项目结构看，最值得优先阅读的是：

- ThemeManager.cs：主题切换主入口
- ThemeManagerExtensions.cs：Application 和 Window 扩展方法
- Theme.cs：主题枚举定义
- Themes/ 下的 XAML：基础样式和各主题资源字典
- Controls/、Converter/、Utilities/：主题库附带的控件、转换器和工具代码

## 关键入口类型

### ThemeManager

ThemeManager 是当前主题模块的中心对象。它负责：

- 维护 CurrentTheme 和 CurrentUITheme
- 处理 UseSystem、Light、Dark、Pink、Cyan 五种主题
- 根据主题装载对应的 ResourceDictionary 列表
- 监听 Windows 主题变化
- 在切换主题时触发主题变更事件
- 调整窗口标题栏颜色

当前资源字典按几组固定列表组织：

- ResourceDictionaryBase：基础共享样式
- ResourceDictionaryDark：深色主题资源
- ResourceDictionaryWhite：浅色主题资源
- ResourceDictionaryPink：粉色主题资源
- ResourceDictionaryCyan：青色主题资源

这说明现阶段的主题机制是“固定主题枚举 + 固定资源字典集合”的实现方式，而不是运行时可任意注册新主题类型的开放模型。

### Theme

当前主题枚举只有五个值：

- UseSystem
- Light
- Dark
- Pink
- Cyan

其中 UseSystem 并不是单独的一套资源，而是在 ApplyTheme 时被映射成当前 AppsTheme 对应的浅色或深色主题。

### ThemeManagerExtensions

ThemeManagerExtensions 提供了两个实际很常用的入口：

- Application.ApplyTheme：应用主题
- Application.ForceApplyTheme：强制重新装载主题资源

另外，Window.ApplyCaption 会在窗口 Loaded 后：

- 设置标题栏颜色
- 根据当前主题切换窗口图标
- 订阅主题变化并在窗口关闭时解绑

所以这个模块不仅管资源字典，也负责一部分窗口壳层外观行为。

## 当前运行时主链

现有主题链路更接近下面这条：

1. 上层 UI 选择主题。
2. Application.ApplyTheme 调到 ThemeManager.Current.ApplyTheme。
3. 如果当前选择是 UseSystem，则先解析成 AppsTheme。
4. ThemeManager 按主题把本模块的资源字典加入 Application.Resources.MergedDictionaries。
5. CurrentTheme 和 CurrentUITheme 更新，并触发变更事件。
6. 已调用 ApplyCaption 的窗口跟随更新标题栏颜色和图标。

## 系统主题跟随是怎样做的

ThemeManager 在构造时会启动一个延迟初始化流程。当前实现会在较晚时机再挂接系统事件，而不是在应用启动最早阶段就同步处理。

它主要监听：

- SystemEvents.UserPreferenceChanged
- SystemParameters.StaticPropertyChanged

然后通过读取注册表中的 Personalize 项判断：

- AppsUseLightTheme
- SystemUsesLightTheme

因此“跟随系统”当前依赖的是 Windows 注册表值和系统事件，并不是框架层自动提供的完整主题同步服务。

## 标题栏颜色和窗口图标

ThemeManager 还负责调用 DWM API 更新窗口外观：

- 深色主题启用沉浸式暗色标题栏
- 粉色和青色主题直接设置标题栏和边框颜色
- 浅色和跟随系统模式重置为系统默认标题栏颜色

Window.ApplyCaption 还会根据当前主题切换窗口图标资源。这部分行为是当前模块很实际的一层价值，旧文档里反而没有把它讲清楚。

## 当前实现的边界

### 主题持久化不由 ThemeManager 自己完成

当前主题配置虽然使用 ColorVision.Themes 命名空间，但配置类 ThemeConfig 实际位于 UI/ColorVision.UI/Themes。

这意味着：

- 主题资源和切换核心在 UI/ColorVision.Themes
- 菜单、快捷键、配置项编辑等集成逻辑在 UI/ColorVision.UI

不要把整个“主题配置系统”都归到 Themes 项目自身。

### 菜单和快捷键入口在 UI 集成层

当前主题菜单和快捷键入口主要在：

- UI/ColorVision.UI/Themes/ThemesHotKey.cs

它负责：

- 生成主题菜单项
- 在切换时写入 ThemeConfig.Instance.Theme
- 调用 Application.ApplyTheme
- 提供 Ctrl + Shift + T 快捷键轮换主题

所以 Themes 模块本身提供的是能力底座，真正和桌面菜单系统对接的是 UI 层。

### 旧文档里的自定义主题扩展点并不存在

当前代码里并没有这些旧文档声称可用的接口：

- Theme.Custom
- ThemeManager.ResourceDictionaryCustom
- ThemeConfig.FollowSystem

这类内容已经不能继续作为现有能力写在 API 参考里。

## 当前更适合怎样读这个模块

### 想看主题如何切换

先看：

- ThemeManager.cs
- ThemeManagerExtensions.cs
- Theme.cs

### 想看主题如何接入应用菜单和配置

先看：

- UI/ColorVision.UI/Themes/ThemeConfig.cs
- UI/ColorVision.UI/Themes/ThemesHotKey.cs

### 想看主题资源长什么样

先看：

- Themes/Base.xaml
- Themes/Dark.xaml
- Themes/White.xaml
- Themes/Pink.xaml
- Themes/Cyan.xaml

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 不存在的自定义主题注册 API
- 伪造的 ThemeConfig 配置字段
- 教程式的完整主题开发流程
- 大段版本号、框架兼容矩阵、性能数字承诺

如果后续要补充主题相关内容，应优先补真实资源字典、窗口行为或 UI 接入点，而不是再恢复成一篇泛化教程。

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
