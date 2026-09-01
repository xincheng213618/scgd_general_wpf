---
knowledge_id: "engine.results"
knowledge_type: "topic"
status: "current"
summary: "区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。"
aliases: ["算法有结果为什么没有叠加层","ViewResultAlg","ResultHandleRegistry","IViewResult","IResultHandleBase","CanHandle1","AlgorithmOverlayManager"]
code_paths: ["Engine/ColorVision.Engine/Services/Core/ViewResultAlg.cs","Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs","Engine/ColorVision.Engine/Abstractions/IResultHandlers.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs","Engine/ColorVision.Engine/Services/Core/ResultImagePresentation.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmManager.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindCrossNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","UI/ColorVision.Algorithms/AlgorithmResults.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayRenderer.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayManager.cs"]
test_paths: ["Test/ColorVision.UI.Tests/AlgorithmResultOverlayTests.cs","Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs","Test/ColorVision.UI.Tests/LocalFindCrossNodeTests.cs","Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs","Test/ColorVision.UI.Tests/ResultImagePresentationTests.cs","Test/ColorVision.UI.Tests/AlgorithmOverlayManagerTests.cs"]
related: ["engine.index","ui.image-editor","algorithms.platform"]
---

# Engine 结果展示链路

先区分三种结果：Engine 的数据库历史结果、统一图像算法的中立 Result/overlay、客户项目业务结果。它们可以显示在同一个 ImageEditor 中，但注册、存储和生命周期不是同一套契约。

## 按问题定位

| 问题 | 入口 |
| --- | --- |
| 历史结果有记录但没图 | `ViewResultAlg.FilePath`、`AlgorithmResultImageDimensions`、文件服务 |
| 历史结果有图但没叠图 | `ResultHandleRegistry`、`CanHandle1`、DAO 和 `Load/Handle` |
| 新统一算法叠图清不掉或误删新叠图 | `AlgorithmOverlayManager`、注册 token、文档 ID 与 source revision |
| 左侧结果表为空 | `ViewResults`、handler 的 `Load` 与列绑定 |
| CSV/MES/Socket 客户字段不对 | 项目 `Process`、Recipe/Fix、exporter，不在通用 overlay 管理器修 |
| 调试按钮打开错算法窗口 | `DisplayAlgorithmManager` 和 `IDisplayAlgorithm`，不是结果注册表 |

## Engine 历史结果契约

| 对象 | 责任 |
| --- | --- |
| `ViewResultAlg` | 主结果 ID、批次、文件路径、模板名、`ViewResultAlgType`、明细集合 |
| `IViewResult` | POI、MTF、SFR、FOV、Ghost 等具体算法明细 |
| `ResultHandleRegistry` | 收集可实例化的 `IResultHandleBase` 派生类型 |
| `IResultHandleBase` | 类型匹配、按需加载明细、表格/叠图展示、可选侧边导出 |
| 项目结果模型 | 客户判定、字段映射和 CSV/MES/Socket 输出 |

`AlgorithmView.listView1_SelectionChanged` 从 `ResultHandles` 取第一个满足 `CanHandle1(result)` 的 handler，然后依次调用 `Load(context, result)`、准备图像坐标空间、`Handle(context, result)`。默认 `CanHandle1` 判断 `CanHandle.Contains(result.ResultType)`，V2 等 handler 可以覆写它并检查结果版本。

主结果与明细的关系是：`AlgResultMasterModel → ViewResultAlg → ResultHandleRegistry/CanHandle1 → Load/DAO → IViewResult → Handle/表格/图元`。不匹配时会清空图像，而不是随意选一个 handler。多个 handler 的匹配条件冲突会受到集合顺序影响，不能以“都能处理同一枚举”作为正确注册。

本地 Flow 算法的“节点失败”和“没有业务结果记录”不是同一语义。`LocalFindLuminousAreaNode` 与 `LocalFindCrossNode` 在 native 检测被拒绝或返回内容校验失败时，先保存一条对应结果类型、非零 `ResultCode` 的主结果和失败原因，再通过 `ResultMessageBus` 发布到算法结果页，随后节点仍按失败结束。失败主记录不生成角点、十字 JSON 文件或结果明细，也不写入 `action.MasterValue(...)`，避免下游把诊断记录当作有效算法输出；图像/批次缺失等执行前置错误仍可能发生在业务结果记录可建立之前。

## 注册与手动算法发现的区别

`Services/ResultHandleRegistry.cs` 在惰性单例首次创建时遍历 `AssemblyHandler.GetAssemblies()`，实例化非抽象的 `IResultHandleBase`。因此需要确认程序集已经进入发现集合、类型可无参构造、匹配条件正确；不能假设之后加载程序集会自动重建这个注册表。

`Services/Devices/Algorithm/DisplayAlgorithmManager.cs` 扫描的是 `IDisplayAlgorithm` 与 `DisplayAlgorithmAttribute`，负责手动算法菜单、创建算法实例和 `DisplayAlgorithmControl`。它**不负责扫描结果 handler**。`Abstractions/IDisplayAlgorithm.cs` 也不是结果注册入口。

## 图像缺失与历史回放

Engine 的 `AlgorithmView` 会先清理旧显示：原文件存在时交给 handler 打开；文件缺失但能恢复尺寸时，用 `ResultImagePlaceholderCache` 建立相同坐标空间的空白底图；尺寸也无法恢复时清空旧图并记录警告。空白底图仅用于恢复标注位置，不能当作原始像素或重新计算算法的证据。

历史结果完成切图后，右键本地算法取得的帧必须对应当前 `ViewBitmapSource`。ImageView 在取帧时会核对缓存所属的源图对象；源图已替换而缓存仍指向上一条结果时，先使旧 revision 失效并从当前位图重建帧。

客户项目可以有自己的历史图片策略，例如保存原图/标注图的回退。不要把某个项目的策略当成 Engine 全局契约；项目页面和项目测试是该策略的权威入口。

## 统一算法 overlay 是另一条链

`UI/ColorVision.Algorithms/AlgorithmResults.cs` 中的结果与 `AlgorithmOverlayArtifact` 是宿主中立数据，不依赖 Engine DAO 或 `IViewResult`。ImageEditor 使用 `AlgorithmOverlayRenderer` 生成图元，由 `AlgorithmOverlayManager` 同时管理图元和 artifact。

| 生命周期条件 | 当前语义 |
| --- | --- |
| 相同名称注册新 overlay | 替换旧项，并用新 token 与 store entry ID 标识新注册 |
| 旧会话释放 | 只有 token 仍匹配时才能移除，不能删除同名的新替代项 |
| source revision 提交 | 清除旧 revision 的 transient 项；persistent 项继续跟随当前文档 |
| 文档替换或清理 | 清理对应文档的图元和 artifact，不残留旧来源数据 |
| 注册释放 | transient 随会话释放；persistent 需显式移除或文档清理 |
| 跨线程变更 | 在画布 Dispatcher 上串行更新，避免状态和可见图元分离 |

这条链详见 [统一图像算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) 和 [ImageEditor](../ui-components/ColorVision.ImageEditor.md)。不要为了显示普通本地算法结果就增加 Engine 历史 DAO，也不要让旧历史 handler 绕过自己的加载契约直接套用中立 Result。

## 新增 Engine 历史结果展示

下面步骤仅适用于接入 Engine 数据库历史结果，不是中立算法 overlay 或客户项目结果的通用实现清单。

1. 明确主结果类型、版本和历史数据兼容规则。
2. 定义实现 `IViewResult` 的明细模型，使用所属模板的 DAO。
3. 实现 `IResultHandleBase`，在 `CanHandle` / `CanHandle1` 中准确匹配。
4. `Load` 负责明细加载；`Handle` 使用 `ViewResultContext` 更新列表和图像。
5. 复用 `UI/ColorVision.ImageEditor/Draw/` 图元与现有坐标转换，例如 `imageView.AddVisual(...)`；不另起独立 Canvas。
6. 客户判定和导出字段保留在项目包；旧数据、缺图与坐标空间都要验证。

## 源码锚点

| 任务 | 仓库相对路径 |
| --- | --- |
| 历史主结果 | `Engine/ColorVision.Engine/Services/Core/ViewResultAlg.cs` |
| handler 契约 | `Engine/ColorVision.Engine/Abstractions/IResultHandlers.cs` |
| handler 注册 | `Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs` |
| 历史显示调度 | `Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs` |
| 缺图坐标恢复 | `Engine/ColorVision.Engine/Services/Core/ResultImagePresentation.cs` |
| 中立结果 | `UI/ColorVision.Algorithms/AlgorithmResults.cs` |
| 统一 overlay | `UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayRenderer.cs`、`AlgorithmOverlayManager.cs` |

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/AlgorithmResultOverlayTests.cs`、`Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs`、`Test/ColorVision.UI.Tests/ResultImagePresentationTests.cs`、`Test/ColorVision.UI.Tests/AlgorithmOverlayManagerTests.cs`。

已登记的是局部绘制、缺图尺寸与统一 overlay 生命周期测试；完整的 handler 扫描、DAO 历史回放、项目输出仍需单独验证，不能把这些局部测试当作全链覆盖。
