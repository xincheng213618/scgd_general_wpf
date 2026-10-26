---
knowledge_id: "algorithms.local-native-analysis"
knowledge_type: "topic"
status: "current"
summary: "本地 Ghost、旋转模板匹配和双目标定融合的直接 native 操作、结果显示与生命周期限制；灯珠定位由独立主题维护。"
aliases: ["直接native分析", "P2", "GhostLocalAnalysis", "M_DetectGhosts", "RotatedTemplateLocalAnalysis", "M_MatchRotatedTemplate", "StereoFusionDebugWindow", "M_CalStereoBinocularFusion", "P2JsonAnalysisWindow", "本地Ghost检测", "旋转模板本地匹配", "双目标定融合", "Ghost 本地分析", "设为旋转匹配模板", "运行融合", "P2 复制结果"]
code_paths: ["UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/P2", "UI/ColorVision.ImageEditor/EditorToolFactory.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/AlgorithmsContextMenu.cs", "UI/ColorVision.ImageEditor/ImageView.xaml.cs", "UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/HImageExtension.cs", "Native/include/algorithm.h", "Native/include/opencv_media_export.h", "Native/include/custom_structs.h", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/exports/p2_export.cpp"]
test_paths: ["Test/opencv_helper_test/test_p2_algorithms.cpp"]
related: ["algorithms.platform", "ui.image-editor", "engine.native-integration", "algorithms.led", "algorithms.light-beads"]
---

# 本地 P2 分析

ImageEditor 提供 Ghost 分析、旋转模板匹配与双目标定融合，适用于对当前图像或矩形 ROI 进行本地分析。这些入口直接调用 `OpenCVMediaHelper` 并展示 native JSON 或标注，不经过[统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)的 Catalog、Runner、实验 provider 门禁或中立 artifact 生命周期。

## 运行前提

准备 Windows/x64 环境、匹配的 `opencv_helper.dll` 及其依赖，并先打开可分析的图像。菜单存在不证明 DLL 已就绪或当前像素格式可执行；像素布局限制见下文。首次构建和 ABI/发布边界见 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)。

## 灯珠定位 {#flow-内存点集与本地测量}

灯珠定位、完整网格不亮与偏暗判定、运行内存 POI 和可选 CSV 的说明见[灯珠定位与完整网格判定](./detectors/light-beads.md#algorithm-principles)。

## P2：Ghost、旋转模板和双目调试

`EditorTools/Algorithms/Calculate/P2/` 是同类直接 native 适配器，但不是灯珠算法的另一组参数。三个全图菜单分别位于“分析测量”的“缺陷与鬼影”“定位与几何”“双目与视区”分组，没有实现 `IAlgorithmCatalogBoundMenu`；它们不自动继承统一 Runtime 的实验 provider 门禁。实际绑定的三个导出实现位于 `Native/opencv_helper/exports/p2_export.cpp`。

| 工具与源码 | 当前输入及执行链 | 结果边界 |
| --- | --- | --- |
| `GhostLocalAnalysis`，`GhostAnalysis.cs` | 当前图像或矩形 ROI → `P2JsonAnalysisWindow` → `M_DetectGhosts` | 原始 JSON、亮源/候选计数、严重度/置信度摘要与 overlay；不经过 MQTT |
| `RotatedTemplateLocalAnalysis`，`RotatedTemplateAnalysis.cs` | 矩形右键“设为旋转匹配模板”或导入参考图/`.cvtm` → `TemplateMatchingWindow` → `TemplateMatching.Run` → `M_MatchRotatedTemplate` | 类型化参数编辑、导入/导出、结果表格与四角叠加；流程节点内置参考图与参数，复用相同内核 |
| `CMStereoBinocularLocalAnalysis`，`StereoFusionAnalysis.cs` / `StereoFusionDebugWindow.xaml.cs` | 当前编辑器图像作左图，选择文件作右图，带标定 JSON 调用 `M_CalStereoBinocularFusion` | 左右五点、三维毫米坐标、视差、重投影误差、置信度及有效状态；不是单图 `M_CalBinocularFusion` |

### 执行 Ghost 或旋转模板匹配

按所需工具选择入口：

- Ghost 分析：在图像右键菜单选择“分析测量 → 缺陷与鬼影 → 鬼影检测（Ghost）...”；局部分析则右键矩形，选择“鬼影检测（Ghost）...”。
- 旋转模板匹配：先绘制模板区域矩形，右键选择“设为旋转匹配模板”；再选择“分析测量 → 定位与几何 → 旋转模板匹配...”，或右键搜索区域矩形选择“在此 ROI 执行旋转模板匹配”。

Ghost 打开参数窗口后：

1. 核对窗口的图像/ROI 信息，在“配置 JSON”中编辑对象参数，点击“运行”。
2. 在“摘要”和“结果 JSON”查看本次状态与结果；`success=false` 或 `statusCode` 报错时，不能把存在 JSON 当作检测通过。
3. 需要保留文本时点击“复制配置”或“复制结果”，内容只写入剪贴板，不自动保存文件。默认勾选“绘制 Overlay”和“自动清除上次结果”；临时叠加与清理范围见下文。

旋转模板窗口使用属性编辑器，支持新建、导入参考图、导入/导出 `.cvtm`；从流程节点打开时，点击“应用到节点”提交配置，再随流程保存，取消或关闭不修改节点。点击“运行匹配”对编辑器打开窗口时的当前图像/ROI 试运行，查看中心、角度、尺度、分数与结果 JSON；新运行、导入和清理会清空旧表格与叠加。源图变化后需重新打开。节点配置契约、默认参数及本地执行边界见 [Matching 模板匹配](./templates/matching-template.md)。

### 执行双目标定融合

1. 在编辑器打开左图，从图像右键菜单选择“分析测量 → 双目与视区 → 双目标定融合...”。
2. 点击“选择右图...”，再点击“加载标定 JSON...”，核对标定与这对图像、尺寸和单位匹配。窗口生成的示例标定不能用于测量。
3. 检查“融合配置 JSON”，点击“运行融合”。查看“三维点”、结果 JSON、状态和警告；毫米坐标需结合点的 `valid`、视差和重投影误差判断。
4. 需要文本结果时点击“复制结果”。更换左图后重新打开窗口核对输入；加载右图、加载配置或复制文本均不代表新一轮融合已完成。

两个 P2 窗口开始新运行时不清空上次 JSON、摘要或点表；配置校验失败、调用异常或新结果被丢弃后，旧内容可能仍可见，“复制结果”也可能复制上一次结果。“清除 Overlay”只清理叠加，不清空结果文本；双目重新选择右图也不重置已保存的结果 JSON。应结合本次运行状态和输入判断，不能用页面仍有数值作为本次成功证据。

P2 矩形入口使用 `P2RoiHelper.TryFromRectangle` 转换 DPI 并与当时图像求交；`Normalize` 只把非正尺寸 ROI 替换为全图，不提供任意 ROI 的通用边界校验。全图双目调试始终向导出传左右完整图像范围。[灯珠定位](./detectors/light-beads.md)的越界回退规则不适用于全部 P2 内核。

### 参数、模板和标定的真实来源

Ghost 窗口允许编辑 JSON 对象，默认参数由适配器的 `CreateDefaultConfig` 生成。Ghost 默认不启用曝光归一化、背景核和方向置信度增强，多尺度层数为 1；native 存在增强实现不等于 UI 默认开启全部增强。旋转匹配改由 `TemplateMatchingOptions` 生成类型化属性编辑器，保存前和调用前验证范围。完整字段继续核对适配器、`p2_export.cpp` 的解析及对应 native 内核，不在源码 README 复制第二份参数表。

旋转模板从当前 `ImageFrameLease.Image` 的 ROI 按行复制像素，避免截取伪彩显示图；会话缓存只作为新窗口的初始参考图。也可导入文件或加载自包含 `.cvtm`，没有参考图时允许打开窗口进行管理，保存和运行则拒绝空模板。运行固定本次参考图和参数，托管层固定像素地址、借用模板 `HImage`，native 返回后释放固定句柄和结果指针；`.cvtm` 保存源样本位深，不保存显示归一化像素。

双目窗口创建的焦距 `1000`、零畸变、单位旋转及平移 `[-60,0,0]` 仅为调试示例，**不能用于真实测量**；源码界面也明确提示加载真实标定。真实 `leftCameraMatrix`、`rightCameraMatrix`、畸变、`rotation` 与 `translation` 必须匹配所用相机、图像尺寸和单位。加载含 `calibration` 对象的文件会替换整份配置；加载单独标定对象则只替换当前配置的 `calibration`。未加载外部标定时，重新选择右图会重新生成默认配置，不能假定手工修改始终保留。

右图通过 `P2BitmapLoader` 读取第一帧：列表内的 WPF 像素格式保留，其他格式转为 Bgra32；随后 `P2ImageSnapshot` 用 `ToHImage` 复制。它不等价于统一 Runner 的格式归一化，RGB/调色板/预乘 Alpha 等布局仍应分别核对具体 native 入口，不把 UI 可打开当作测量精度保证。

### 完成、取消与临时叠加

P2 参数窗口只在点击运行时计算，不是滑动预览。适配器重新获取当前帧租约，在 `Task.Run` 中同步调用 native 并等待结果；调用后检查图像 revision。窗口另检查是否已关闭；双目窗口还核对右图是否仍是启动该次运行时的同一对象。它们不使用 `ImageAlgorithmPreviewSession`，不能据此推断 host-wide invocation 的 latest-wins、像素提交或统一 artifact 所有权。

Ghost 和双目的 revision 校验从每次运行开始。Ghost 的搜索 ROI 在打开窗口时确定，而运行时重新取当前帧；双目左预览和尺寸文案只在构造时赋值，运行却使用新的左图租约。因此运行前换图后，这两个窗口可能仍显示旧预览/ROI/标定而计算新图，应重新打开核对。旋转模板窗口则记录打开时 revision，运行前拒绝换图后的输入，运行后再次核对 revision。双目对右图对象身份的检查位于成功结果发布路径，异常提示分支只检查左图 revision 和关窗状态，不能保证换右图后旧运行的错误提示也被丢弃。

`P2NativeJson.Invoke` 要求返回长度为正且指针非空，按 UTF-8 读取对象 JSON，并经 `FreeResult` 释放。这只证明拿到了 JSON，仍须读取 `success`、`statusCode`、`message`、`warnings` 及具体点的 `valid`/质量指标；例如无效常量模板可以返回成功分配但 `success=false` 的 JSON。

关闭窗口只设关闭标志、清理当前记录的叠加并拒绝之后的显示，没有给正在运行的 native 调用传取消令牌，也不等待其停止。不要把关窗当作释放全部 native 资源的即时完成信号；输入租约与快照在后台调用退出后才释放。一个窗口禁用自己的运行按钮也不代表其他窗口或同图调用受到统一排他控制。

P2 overlay 通过 `DrawCanvas.AddOverlayVisual` 添加临时 `DrawingVisual`，不提交 source 像素、不自动保存 JSON/图片，也不是 FindLightBeads 的可撤销批量图层。Ghost 窗口勾选“自动清除上次结果”且绘制新叠加时，先移除上一次叠加；关闭该选项反复运行时，`_overlay` 字段只保留最新一项，清理/关闭只移除这项，先前叠加可能残留。旋转模板窗口始终在新运行前清除自己上一轮叠加，关窗也清除此窗口的叠加。双目窗口另清理左右点预览和自己记录的画布叠加，不代表清空整个画布。

双目点预览还有当前代码限制：`ApplyResult` 为 `LeftPointOverlay` / `RightPointOverlay` 设置结果后，紧接着的 `ClearOverlay()` 又清空二者，随后只重建主画布的左图叠加。因此不能承诺成功计算后窗口内的左右点标记保留可见；JSON、点表与主画布叠加是不同显示路径。

## 验证依据与缺口

`Test/opencv_helper_test/test_p2_algorithms.cpp` 有合成 Ghost、旋转/缩放/遮挡匹配、常量模板拒绝、已知标定五点双目和导出失败清空指针等用例。测试源码存在不代表本次运行过，也不覆盖真实相机标定、完整 WPF 菜单、模板替换、关窗期间 native 执行或关闭自动清理后的叠加残留。这些 UI/交付边界仍需专门验收；不要用统一 Runner 测试替代。
