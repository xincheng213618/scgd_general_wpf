---
knowledge_id: "ui.image-editor-settings-plan"
knowledge_type: "decision"
status: "current"
summary: "图像设置的作用范围、显式默认值与标定档案保存、当前视图隔离和扩展协议；主设置独立入口与旧接口清理仍待实施。"
aliases: ["图像设置重构", "图像设置规划", "当前有效", "全局与当前", "外接设置", "设置作用范围", "设置提供方", "ImageViewSettingsEntry", "ImageViewSettingsWindow", "标定状态隔离", "ImageSettingsSession"]
code_paths: ["UI/ColorVision.ImageEditor/Settings", "UI/ColorVision.ImageEditor/ImageView.xaml.cs", "UI/ColorVision.ImageEditor/ImageViewConfig.cs", "UI/ColorVision.ImageEditor/EditorTools/Filters", "UI/ColorVision.ImageEditor/EditorTools/PseudoColor", "UI/ColorVision.ImageEditor/Draw/Ruler", "UI/ColorVision.ImageEditor/Draw/Text/DefalutTextAttribute.cs", "UI/ColorVision.ImageEditor/Draw/Special/ToolReferenceLine.cs", "UI/ColorVision.UI/PropertyEditor/SettingsPropertyPresenter.cs", "Engine/ColorVision.Engine/Media/CvcieDisplaySettingProvider.cs", "Engine/ColorVision.Engine/Media/CvcieMouseProbeSettingProvider.cs", "Engine/ColorVision.Engine/Media/CvcieMouseProbeOptions.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageSettingsScopeTests.cs", "Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs", "Test/ColorVision.UI.Tests/DrawingVisualScaleHostTests.cs", "Test/ColorVision.UI.Tests/RealtimePseudoColorServiceTests.cs", "Test/ColorVision.UI.Tests/SelectEditorRenderOptimizationTests.cs"]
related: ["ui.image-editor", "ui.image-editor-context", "ui.configuration", "ui.property-grid", "engine.file-io"]
---

# 图像设置：作用范围、保存和扩展

图像设置窗口绑定一个 `ImageView`，按照设置影响的对象组织页面。应用配置可以共享；当前视图的滤镜、标定和工具偏好由视图各自持有。提供方与作用范围是两个独立维度：Engine 扩展既可以提供全局默认值，也可以提供当前视图设置。

## 页面与生效范围

| 分区 | 页面与内容 | 生效和保存 |
| --- | --- | --- |
| 当前视图 | 显示、工具栏 | 立即影响此视图，不持久化 |
| 当前视图 | 伪彩、显示滤镜、适用的 CVCIE 探针 | 局部状态；提供“应用默认值”和“将当前设为默认” |
| 当前视图 | 标定与单位 | 使用此视图的有效标定；“保存到此来源档案”才更新档案 |
| 应用偏好 | 显示与性能 | 共享 `DefaultImageViewDisplayConfig`；已有订阅者响应修改 |
| 应用偏好 | 创建时默认值 | 新视图缩放、滤镜、伪彩、探针及新标注文字的初始化值，不广播覆盖已有局部状态 |
| 应用偏好 | 文件打开 | TIFF 和 Engine CVCIE 读取/显示策略；当前文件不会因修改配置自动重读 |
| 应用偏好 | 实时图像 | 由支持 `DefaultRealtimeCameraConfig` 的实时入口使用 |
| 当前图像 | 图像信息、扩展与诊断 | 只读摘要、技术属性、打开器和支持后缀，提供复制与刷新 |
| 扩展 | 尚未声明作用范围的旧条目 | 显示外部管理提示，保存委托只由该条目的显式按钮调用 |

窗口按稳定页面 ID 导航，只有旧兼容条目回退到分组文本。搜索匹配页面、条目和可编辑属性名称。普通页面使用一个主滚动容器；当前值与默认值不依据“恰有两个条目”自动左右并排。

`SettingsPropertyPresenter` 位于 `ColorVision.UI`，沿用现有属性编辑器、验证和可见性绑定，提供统一说明和紧凑控件列；同时读取 `Display` 资源元数据及 `DisplayName / Description`。ImageEditor 不依赖 Desktop 或 Engine。主应用设置尚未迁移到这个公共行呈现器。

## 局部状态和默认值

`ImageViewConfig.Configs` 是每个视图的类型字典；`Properties` 是随图像清除的附加字典。它们的生命周期及字符串键限制见[编辑器上下文](../../04-api-reference/ui-components/image-editor-context.md)。不要把 scope 标签当成独立存储空间。

### 显示滤镜

`DisplayShaderFilterEditorTool` 构造时复制 `DisplayShaderFilterDefaultConfig.State` 到独立 `State`。调节、普通窗口关闭和工具释放都不会自动写入全局默认值；`SaveAsDefault` 才显式复制并保存。修改默认值只影响后续创建的视图，已有视图通过 `RestoreDefaults` 主动应用。

工具栏设置按钮打开统一图像设置窗口的滤镜页。旧 `DisplayShaderFilterWindow` 类型保留给兼容调用方，但不再是这个工具的默认入口。

`AttachPersistence(state, saveAction)` 保留外部宿主的显式持久化契约；该模式继续防抖写回宿主目标，并在页面提示外部管理。普通构造不自动附着全局存储。替换附着目标会取消旧计时器并推进保存代次，释放时取消排队回调。该接口没有取消/事务语义；宿主委托的失败契约仍由宿主负责。

### 伪彩与探针的换图行为

同一视图切换图像时保留伪彩配色与自动范围偏好，但关闭效果并按新图像位深重置范围，需重新启用效果。默认值只在工具创建或显式应用时读取。自动范围在新源已赋值后计算；实时帧的 generation 防护继续由原 controller 管理。

CVCIE 探针对象缓存在 `Configs`，切换文件不丢失此视图的半径/形状偏好。初次创建从全局默认值复制；旧 `Properties[ViewStateKey]` 对象可被收纳到新的视图缓存。全局默认页无需先打开 CVCIE；局部探针条目只在适用图像中注册。

### 标定

`ImageCalibrationConfig.Profiles` 保存档案；每个 `ImageViewConfig.Calibration` 保存独立有效值。该对象由比例尺、画布中的 `DrawingVisualRuler` 和参考网格共同读取，修改一个视图不会影响另一个视图。

`ApplyToView` 解析显式 `CalibrationSourceKey`，否则使用相机厂家/型号键，再回退 `Default`。`CalibrationProfileKey` 保存解析结果，不能回填成显式输入键，否则后续相机信息会被旧键遮蔽。读取未知来源回退默认档案而不创建持久档案；同一来源再次应用默认保留局部调整，`reload: true` 才重新应用档案。来源变化只更新所属视图。

“保存到此来源档案”检查窗口捕获的档案键仍对应当前来源，然后从该视图的标定对象保存。厂家/型号组合不保证设备唯一；需要设备级档案时由宿主提供稳定键。既有配置类型、`Default` 和来源键格式保留，不自动重命名历史配置。

未绑定视图的旧比例尺和尺子静态 API 仍使用 `DefalutTextAttribute.Defalut` 作为兼容回退。新代码必须从上下文取得标定，不继续使用静态有效值。物理长度只接受正有限值，其他值归一到 1。

## 保存与错误

`ImageSettingsSession` 在窗口创建时捕获已声明为 `Application / Defaults` 且有保存委托的目标。它仅序列化这些配置对象以比较改动，不遍历编辑器、图像缓冲或任意运行时对象。

- “保存更改”和“完成/关闭”只保存已改动的持久目标；未修改窗口和只读信息浏览不产生存储调用。
- 一个目标成功保存后更新比较基线；保存后再关闭不会重复写入。部分目标失败时，只保留失败目标等待重试。
- “将当前设为默认”是独立动作，成功后同步该默认目标的基线；当前值不会因为普通关闭而升级为默认值。
- 输入验证失败会定位对应页面并阻止保存/完成。存储失败保留窗口并显示错误；用户可重试或选择“关闭，稍后再保存”。后者不回滚已即时生效的内存改动，也不保证它们已经落盘。
- `ImageSettingsPersistence.Save` 检查当前配置服务仍拥有页面绑定的同一实例。`ConfigHandler` 使用返回结果的 `TrySave`；其他 `IConfigService` 沿用其保存契约，失败需抛出异常。无存储服务或实例已被替换时，显示相应错误而不报告成功。
- 旧条目未声明范围时不在关闭时自动调用保存委托。它们需要显式保存按钮，提供方应逐项补齐范围和目标身份。

窗口不在关闭时重新枚举 provider。图像清除或档案来源改变后，旧图像/档案/兼容扩展页禁用并提示重新打开；当前视图工具偏好和已捕获的全局目标仍可使用。视图释放后局部页也禁用。窗口关闭时解绑配置、图像事件和修改跟踪。

## 扩展协议

`ImageViewSettingsEntry` 保留原构造签名，并补充 `Id`、`OwnerId`、`CategoryId`、`Scope`、`Order`、说明、只读标志、属性白名单、可选视图工厂和显式动作。

`RegisterSettings` 保留旧入口；需要卸载的提供方使用返回 `IDisposable` 的 `RegisterSettingsProvider`。窗口逐个隔离 provider 异常，以 `OwnerId + Id` 去重并按顺序展示。视图释放清空注册；注册句柄重复释放无副作用。

保存委托应捕获正在展示的对象，而不是执行时再次访问活动视图。例如全局默认配置：

```csharp
var config = CvcieDisplayConfig.Current;
new ImageViewSettingsEntry(SettingsText.FileOpening, "CVCIE", config,
    () => ImageSettingsPersistence.Save(config))
{
    Id = "cvcie-display",
    OwnerId = "Engine",
    CategoryId = ImageSettingsCategories.FileOpening,
    Scope = ImageSettingsScope.Defaults,
    Description = SettingsText.FileHint
};
```

外接模块与 ImageEditor 只共享协议，不共享业务控制器。自定义 `CreateView` 必须自己保证只读契约及资源生命周期；通用宿主无法推断专用控件是否写入数据。

## 后续迭代

以下内容尚未实施，不能作为当前能力：

1. 将无视图依赖的默认值提供方抽出，在主应用设置中复用；打开全局默认页不需要创建完整 `ImageView`。
2. 逐项迁移第三方旧条目，清理静态标定 API 和旧专用窗口；保持外部二进制兼容，不能仅按仓库内无引用删除。
3. 为特殊源/图层建立更明确的身份与有效范围策略，并刷新已打开窗口的适用页面。目前来源变化采用禁用旧目标并重新打开的保守策略。
4. 在共用行布局稳定后迁移 Desktop 设置；如需真正“取消”，应另行引入可回滚编辑事务，不能给当前即时编辑换一个按钮名称。

## 验证

`ImageSettingsScopeTests` 覆盖双视图 Shader/标定隔离、显式默认值、来源切换、探针/伪彩换图、仅保存改动、失败重试、无修改关闭零写入、注册注销及配置实例替换。`CvcieDisplaySettingsTests` 覆盖无图可见、共享全局对象和文件打开页归属。

相邻回归入口是比例尺弱事件、尺子渲染、实时伪彩 generation、打开完成及工具工厂生命周期测试。控件布局和主题需使用实际 WPF 资源验证；输出更高 DPI 的截图不等同于跨显示器 DPI 运行验收，真实设备图像与外部二进制宿主也需分别验证。
