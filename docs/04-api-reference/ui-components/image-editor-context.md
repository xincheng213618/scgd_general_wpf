---
knowledge_id: "ui.image-editor-context"
knowledge_type: "topic"
status: "current"
summary: "ImageEditor 的状态归属、扩展构造、工具刷新与临时 ROI 有效期；区分配置分类、图像版本和真实像素坐标。"
aliases: ["图像编辑器上下文", "工具栏刷新", "配置作用域", "临时ROI", "临时选区", "选区坐标", "EditorContext", "ImageProcessingContext", "ImageViewConfig", "ImageViewPropertyScope", "IEditorToolFactory", "BeginSelectAsync", "TransientRoiSelectionSession", "ImageSelectionScope", "EnableEditorImageServices"]
code_paths: ["UI/ColorVision.ImageEditor/ARCHITECTURE.md", "UI/ColorVision.ImageEditor/EditorContext.cs", "UI/ColorVision.ImageEditor/Contexts/ImageProcessingContext.cs", "UI/ColorVision.ImageEditor/ImageViewConfig.cs", "UI/ColorVision.ImageEditor/ImageViewPropertyMetadata.cs", "UI/ColorVision.ImageEditor/EditorToolFactory.cs", "UI/ColorVision.ImageEditor/ImageView.xaml.cs", "UI/ColorVision.ImageEditor/TransientRoiSelectionSession.cs", "UI/ColorVision.ImageEditor/EditorTools/PseudoColor", "UI/ColorVision.UI/AssemblyHandler.cs"]
test_paths: ["Test/ColorVision.UI.Tests/EditorToolFactoryLifecycleTests.cs", "Test/ColorVision.UI.Tests/TransientRoiSelectionSessionTests.cs"]
related: ["ui.image-editor", "ui.discovery", "ui.configuration", "algorithms.platform", "algorithms.roi-routes", "algorithms.local-native-analysis"]
---

# ImageEditor：上下文、工具装配与临时选区

本页维护每个 `ImageView` 的状态与扩展契约。文件打开完成、撤销、保存、视频和 3D 见 [ImageEditor 输出主题](./ColorVision.ImageEditor.md)；这里的配置分类和刷新方法不构成持久化、重新发现扩展或算法完成保证。

## 状态由谁持有

`ImageView.CreateEditorContext` 为视图创建 `ImageViewConfig`、`DrawEditorContext` 和 `ImageProcessingContext`，再由 `EditorContext` 聚合。`EditorContext` 不是任意服务的注册容器：绘图列表、选择态、画布和缩放主要转发给绘图上下文，文本编辑上下文按需创建。

`ImageProcessingContext` 通过 binding 委托访问宿主的 `DocumentInstanceId`、`ImageRevision`、`IsDisposed`、`ViewBitmapSource`、`FunctionImage` 与帧获取/修改入口，不维护第二份独立文档。它持有所选 `AlgorithmRuntime`、该 runtime 的 invocation coordinator 和本上下文的 overlay manager。不能用另一份位图或文档的版本号为当前结果背书。

`ViewBitmapSource` 是当前基准位图，`FunctionImage` 是处理/预览显示层；直接赋这些属性不等同于调用像素提交入口，也不自动推进版本。异步结果发布需遵守文档身份、source revision 和释放状态检查，详见[统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)。

### 配置分类不是隔离容器

`ImageViewConfig.Properties` 是一个按字符串键索引的字典。当前 `ImageViewPropertyScope` 只有 `ImageMetadata`、`ViewState`、`OpenerRuntime` 三项，没有旧稿中的 `Legacy`。

- `SetImageMetadata`、`SetViewState`、`SetOpenerRuntime` 共用 `SetProperty`；同名键会覆盖原值及 scope/owner/description。scope 和 owner 是说明元数据，不是命名空间或权限隔离。新键应避免与已有键冲突。
- `GetProperties<T>` 只接受值本身属于 `T`，否则返回默认值，不自动转换。直接写公开 `Properties` 字典可能绕过或留下旧的分类元数据；没有分类记录的条目在 `GetPropertyEntries` 中归为 `ViewState`。
- `ClearProperties` 清空文件路径、全部属性及分类记录，再触发 `Cleared`，不是只清 `ImageMetadata`。`ClearCommand` 本身只发 `Cleared` 通知，并不调用 `ClearProperties`；实际后果还取决于订阅者。
- `Configs` 是另一份按精确 `Type` 缓存的 `IImageEditorConfig` 字典，`GetRequiredService<T>` 缺项时构造并缓存。`ClearProperties` 不清这份字典；它也不等于全局 `ConfigService` 的配置实例或保存入口。`Properties` 与分类记录标了 `JsonIgnore`，不能把临时属性写入当成已保存。

### 关闭编辑服务也会改变图像文档

`SetImageSource` 有单参入口和三参 `(source, enableEditorImageServices, configureDefaultLayerController)` 入口，没有二参重载。它总会先登记 `ImageSourceReplaced`、重置伪彩并清旧源，再检查/设置新像素源。`enableEditorImageServices=false` 主要关闭图层选择以及本次伪彩图像配置和默认图像校准应用，不会跳过文档版本推进、源替换、像素元数据、加载通知或状态栏刷新。因此 `EnableEditorImageServices=false` 不是只读显示沙箱；不支持的像素格式也可能在旧源已清除后抛出异常。

伪彩由 `PseudoColorEditorTool` 持有 state/controller，并通过工厂查找，不是向 `EditorContext` 注册旧稿中的 `IPseudoColorService`。构造视图会创建这些工具；不要从“尚未打开图像”推断没有初始化副作用。

## 扩展发现、构造与刷新

`EditorToolFactory.cs` 中的 `IEditorToolFactory` 实际是类。构造时各扩展点走不同发现入口，并非全部统一为无参反射：

| 扩展点 | 当前构造约束 |
| --- | --- |
| `IDVContextMenu` | `AssemblyHandler` 的程序集/类型集合；优先可解析上下文构造，无匹配时才尝试 public 无参构造 |
| `IIEditorToolContextMenu`、全局 `IEditorTool` | `Application.Current.GetAssemblies()` 与 `AssemblyHandler.GetTypes`；要求可解析的 public 上下文构造，不提供无参回退 |
| `IImageComponent` | `AssemblyService.LoadImplementations<IImageComponent>()`；不同于上述上下文注入通道 |
| `IImageOpen` | `AssemblyService` 程序集内查 `FileExtensionAttribute`，对每个后缀用 `Activator.CreateInstance(type, context)` 创建实例；后缀转小写后 `Dictionary.Add`，重复后缀会冲突，不是后者自动覆盖 |

上下文构造只匹配 `EditorContext`、`DrawEditorContext`、`ImageProcessingContext`、`DrawCanvas`、`TextEditingContext`、`ImageViewConfig` 六种精确类型；选择参数最多且所有参数可解析的 public 构造。它不是通用 DI，不自动匹配基类、任意接口或新增服务。`AssemblyHandler` 已缓存程序集/类型，不能照旧稿宣称每个视图都做完全无缓存的全量反射；实例装配与类型缓存仍是两件事。

只有实现 `IAlgorithmCatalogBoundMenu` 的菜单走 runtime descriptor/adapter/capability 门禁；不能把该门禁泛化到所有右键菜单或直接 native 工具。后者见[本地 Native 分析](../algorithms/local-native-analysis.md)。

`RefreshToolBars` 只移除工厂自己生成的 UI 元素，并从现有工具集合重新装配；它不重扫程序集、重建打开器或发现新加载插件。初始化时工厂早于 `Crosshair` 创建，随后才执行 `IImageComponent.Execute` 等步骤；扩展构造不能假定所有视图服务都已就绪。

打开器通过 `IImageOpenEditorToolProvider` 贡献当前工具。`GetEffectiveEditorTools` 先放打开器工具，再加入未被其非空 `GuidId` 覆盖的全局工具；比较区分大小写，空 ID 不参与覆盖，也不会自动去重打开器内部的重复 ID。`ApplyImageOpenTools` 先通知旧 lifecycle 停用、替换集合和刷新工具栏，再通知新 lifecycle 启用；停用不等于所有旧工具已 `Dispose`。

工厂 `Dispose` 停用当前打开器 lifecycle，并对全局及当前打开器工具中的 `IDisposable` 去重释放，不承诺释放每个 opener/component。`ImageView.Unloaded` 仅解绑窗口快捷键，不是 `Dispose`；宿主仍需负责真正释放。工具栏重建、控件卸载和文档资源释放不能混用。

## 临时 ROI：形状、坐标与有效期

`ImageView.BeginSelectAsync` 每次创建一个 `TransientRoiSelectionSession`，支持 Rectangle、Circle、Polygon、Quadrilateral。临时 visual 直接加入/移出画布，不登记撤销命令，也不是持久注释。

- 位置来自 `e.GetPosition(DrawCanvas)`，是 WPF 画布坐标；此类没有统一进行原始像素换算或边界裁剪。非 96 DPI、图像变换或裁剪后不能直接把坐标当源像素 ROI，需按调用链明确转换，见 [ROI 路由](../algorithms/primitives/roi.md)。
- 矩形/圆拖拽结束但宽高太小或非有限值时，重置这次尝试、继续等待，不立即返回 `null`。无效多边形也继续等待，不能依赖旧方法注释推断退化形状都会取消。圆以按下点为中心、移动距离为半径。
- Escape 或绑定源失效时清理并返回 `null`；右键/键盘完成行为需按具体形状分支核对。正常启动后完成/取消会解绑事件、删除临时 visual、释放鼠标捕获并恢复记录的交互状态。初始源就无效时，`Start` 在保存旧状态之前进入清理，当前可能用默认值覆盖 cursor/ActivateOn；立即返回 `null` 不保证交互状态未变，这是实现缺口而非推荐行为。
- 绑定 `ImageProcessingContext` 时捕获不可变 `ImageSelectionScope`：文档身份、source revision、像素宽高和 DPI。选取期间换图、源版本变化或文档释放使 session 取消；成功结果携带该 scope。单独构造无处理上下文的绘图 session 则不能提供同样的图像绑定保证。
- 已完成结果不会在日后换图时自动消失。调用方须保留 `SelectResult.SourceScope`，使用 `ImageAlgorithmInputFactory.Acquire(context, scope)` 等入口再次核验；不能丢掉 scope 后复用旧坐标。每次调用各建 session，不保证新调用自动取消旧调用，也没有统一单活动 session 调度器。

## 验证范围

`EditorToolFactoryLifecycleTests` 覆盖重复工具栏刷新时图标元素复用，不覆盖任意插件、重复后缀或所有构造失败。`TransientRoiSelectionSessionTests` 覆盖退化/自交形状、完成后 scope、版本变化/释放取消及后续输入获取拒绝过期范围；部分通过反射驱动内部状态，不等于真实鼠标和任意 DPI 的整链验收。配置同名键、实际工具发现与真实窗口行为仍需按改动补验证。文档引用测试不表示本轮运行了 WPF 或 native 测试；只读知识问答无需启动产品或连接设备。
