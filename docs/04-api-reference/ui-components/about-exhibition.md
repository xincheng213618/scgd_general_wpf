---
knowledge_id: "ui.about-exhibition"
knowledge_type: "topic"
status: "current"
summary: "主程序的图像、色彩与测量展示，以及 Spectrum 的光谱关于页：独立品牌和版本、中英文与繁体资源、不透明深浅配色、原生圆角对齐与关闭释放约束。"
aliases: ["关于页面", "关于窗口", "炫技界面", "光谱之间", "关于窗口黑边", "关于页中英文", "AboutMsg", "AboutMsgWindow", "AboutArtScene", "AboutArtwork", "AboutWindowChrome", "AboutText", "AboutResources", "VisionImageStudy", "SpectrumAboutWindow", "光谱粒子", "NOCTURNE", "OPALINE"]
code_paths: ["ColorVision/AboutMsg.xaml", "ColorVision/AboutMsg.xaml.cs", "UI/ColorVision.UI/Views/About/AboutArtScene.cs", "UI/ColorVision.UI/Views/About/VisionImageStudy.cs", "UI/ColorVision.UI/Views/About/AboutWindowChrome.cs", "UI/ColorVision.UI/Views/About/AboutText.cs", "UI/ColorVision.UI/Views/About/AboutResources.resx", "UI/ColorVision.UI/Views/About/AboutResources.en.resx", "UI/ColorVision.UI/Views/About/AboutResources.zh-Hant.resx", "Plugins/Spectrum/Help/SpectrumAboutWindow.xaml", "Plugins/Spectrum/Help/SpectrumAboutWindow.xaml.cs"]
test_paths: []
related: ["ui.themes", "ui.hotkeys", "plugins.spectrum"]
---

# 关于窗口：视觉展示与光谱展示

主程序 **帮助 → 关于** 通过 `AboutMsgExport` 打开 `AboutMsgWindow`。左侧突出文字品牌 **ColorVision**；简体中文显示“光电视觉检测平台”和“让视觉，成为判断。”，英文显示“OPTICAL VISION INSPECTION”和“Turn vision into insight.”。右侧将同一幅程序生成的光学图像分成色彩图、像素采样与轮廓测量三个平面，用 ROI、尺寸辅助线和少量流程连线表达图像、色彩、测量、流程之间的关系。主窗口下沿以手写字体 **Xin** 署名，无前缀、中文姓名或悬停提示。

Spectrum 窗口的 **帮助 → 关于 Spectrum** 通过仅面向 `Spectrum` 的 `MenuSpectrumAbout` 打开 `SpectrumAboutWindow`，采用扭转光谱环和波长装饰。两者都是由各自活动窗口拥有的模态展示页，不提供业务操作或显示底部进度条式彩线。

关于页的四个展示类与三份语言资源由 `UI/ColorVision.UI/Views/About` 维护单份源码；Spectrum 通过源码链接将它们编入自身的 `Spectrum.Help.Art` 命名空间及语言资源程序集，避免插件依赖已发布宿主尚未包含的关于页类型，主程序继续使用 `ColorVision.UI.Views.About`。

页面分别读取 `ColorVision` / `Spectrum` 自身的程序集版本和文件本地修改日期，并显示 .NET 运行时和进程位数。构建日期是文件时间，不是可追溯构建标识。主程序保留文字品牌署名，不显示软件图标、CPU/GPU 型号或硬件 ID，也不在打开时查询硬件；Spectrum 展示页同样不连接设备或加载标定。

## 语言资源

两个窗口的产品文案、标题、辅助功能名称和按钮提示由共享 `AboutText` 的 14 个静态属性提供；Spectrum 的“关于 Spectrum”菜单标题也读取 `AboutText.SpectrumTitle`。品牌名称、设计署名与装饰性英文可以保留，中文布局有意采用中英混排。

`AboutText` 使用独立的 `ResourceManager` 读取 `AboutResources.resx`（简体中文默认资源）、`AboutResources.en.resx` 和 `AboutResources.zh-Hant.resx`。每次读取依据 `CultureInfo.CurrentUICulture`：中文文化保留标准父文化回退，`zh-TW`、`zh-HK` 等进入 `zh-Hant`，简体文化回退到默认资源；英语及其他非中文文化在本组件内选择英文，避免尚无翻译的非中文文化回落到中文。该选择不修改全局 `Resources.Culture` 或 `LanguageConfig`。

XAML 通过 `x:Static` 在窗口构造时取值，不订阅额外的即时语言变更事件。软件切换语言继续沿用 `LanguageManager.LanguageChange` 的保存配置并重启流程，已经打开的关于窗口不承诺动态替换文案。

## 配色与窗口行为

- 每次打开采用 `ThemeManager.Current.CurrentUITheme`：软件当前为深色则进入深色，当前为浅色则进入浅色。右上角半圆按钮仅切换本窗口的深浅配色，不修改全局主题或保存配置；关闭后不保留局部配色，重新打开时再次跟随软件当前主题。窗口打开期间收到应用实际主题变更时也重新跟随。只有 Spectrum 展示深夜（NOCTURNE）和珠光（OPALINE）名称，主程序不显示作品编号或配色标签。
- 两个展示窗口直接继承 WPF `Window`，使用自己的模板和 `WindowChrome`，不依赖 `BaseWindow` 的标题栏、模糊与窗口命令。表面固定不透明，`GlassFrameThickness=0`，不跟随全局 `ThemeConfig.TransparentWindow`。光晕只在页面内部混合，避免背后窗口改变底色和文字对比度。公共窗口机制见[窗口主题契约](./ColorVision.Themes.md)。
- 主程序采用 880 × 530 的紧凑逻辑布局，Spectrum 保持 880 × 590。两个窗口构造时调用 `AboutWindowChrome.FitToWorkArea(this, 16)`，按工作区限制等比缩小，内部 `Viewbox` 保持构图。该 helper 将视觉半径转换为 `WindowChrome` 传给原生圆角区域的椭圆直径，并同步布局缩放；DPI 转换由 WPF 处理。窗口模板保持完整的不透明底色，内层作品单独裁切圆角，避免原生窗口区域与可见圆角不一致时露出黑角。
- 拖动空白区域移动窗口；右上角关闭按钮和 Esc 关闭窗口，关闭时激活父窗口。拖动由窗口处理未被子控件消费的鼠标按下事件，不影响按钮点击。按钮使用自身焦点边框，不叠加默认虚线焦点框。失焦保留画面并暂停运动，重新激活继续。
- 版本和构建信息位于下沿。主程序的光学图像、像素与测量标记，以及 Spectrum 的光谱刻度与波长范围，均为艺术构图，不表示当前设备图像、测量数据或流程执行状态。

## 渲染和资源生命周期

公共 `UI/ColorVision.UI/Views/About/AboutArtScene.cs` 管理两种作品的配色、鼠标缓动和动画生命周期。`AboutArtwork.Vision` 委托内部 `VisionImageStudy` 绘制同源图像的三个平面，保持图像可辨认的轻微视差；`AboutArtwork.Spectrum` 使用扭转环面的采样点，经过透视投影与深度排序后绘制线框、漂浮粒子和游走高光。

绘制通过 WPF `DrawingContext` 完成，不依赖主程序、Engine、真实图像文档或设备，使 Spectrum 独立包也能使用。图像与装饰由程序生成，没有外部图片、视频、着色器文件或新增运行依赖；可复用的背景绘图与画刷冻结并缓存。

`CompositionTarget.Rendering` 的重绘请求上限为 30 帧/秒，不保证每台机器达到该帧率。失焦、隐藏、卸载、禁用系统客户端动画、系统高对比度或软件渲染层级 0 时停止持续重绘。高对比度使用系统文字/背景色并隐藏装饰场景。

窗口关闭时解除主题和系统设置订阅；场景卸载时解除合成、系统设置和渲染层级事件，停止计时。主题订阅保存最初的发布者，关闭不从可能被替换的 `ThemeManager.Current` 解绑。

## 验证

视觉验收覆盖简体、繁体、英文与非中文界面文化，核对产品名称、窗口标题和程序集版本；本地配色切换不应修改全局主题，关闭重开后应采用应用当前主题。

同时检查深浅配色、鼠标视差、窗口拖动、暂停与恢复、关闭后订阅释放，以及不同 DPI 下原生圆角与内部构图的一致性。资源与源码检查不能证明实际帧率或场景回收。
