---
knowledge_id: "algorithms.local-native-analysis"
knowledge_type: "topic"
status: "current"
summary: "ImageEditor直接native灯珠与P2分析：Ghost/旋转模板/双目标定、缺失计数与完成边界；区别Engine/MQTT模板和统一Runner。"
aliases: ["FindLightBeads", "M_FindLightBeads", "FindLightBeadsConfig", "BlackCenters", "MissingCount", "本地灯珠检测", "直接native分析", "P2", "GhostLocalAnalysis", "M_DetectGhosts", "RotatedTemplateLocalAnalysis", "M_MatchRotatedTemplate", "StereoFusionDebugWindow", "M_CalStereoBinocularFusion", "P2JsonAnalysisWindow", "本地Ghost检测", "旋转模板本地匹配", "双目标定融合"]
code_paths: ["UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLightBeads/FindLightBeadsCM.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLightBeads/README.md", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/P2", "UI/ColorVision.ImageEditor/EditorTools/GraphicEditing/GraphicEditingWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorToolFactory.cs", "UI/ColorVision.ImageEditor/ImageView.xaml.cs", "UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/HImageExtension.cs", "Native/include/algorithm.h", "Native/include/opencv_media_export.h", "Native/include/custom_structs.h", "Native/opencv_helper/algorithm.cpp", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/exports/p2_export.cpp"]
test_paths: ["Test/ColorVision.UI.Tests/AlgorithmCircleOverlayRenderOptimizationTests.cs", "Test/opencv_helper_test/test_p2_algorithms.cpp"]
related: ["algorithms.platform", "ui.image-editor", "engine.native-integration", "algorithms.ghost", "algorithms.led"]
---

# ImageEditor 直接 native 分析

ImageEditor 中仍有通过专用菜单直接调用 `OpenCVMediaHelper`、取得 native JSON 后自行展示的分析入口。本页记录这条兼容链的真实输入、完成和结果边界；不能因为它位于 `EditorTools/Algorithms/`，就认定它经过[统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)的 Catalog、Runner、实验 provider 门禁或中立 artifact 生命周期。

本地 `FindLightBeads` 也不是 Engine 的 `FindLED`/MQTT 模板，不读取历史结果 DAO。远端灯条/灯珠契约见 [LED 检测模板](./templates/led-detection.md)。

## 灯珠入口与运行前提

`FindLightBeadsCM.cs` 提供两个入口：`CMFindLightBeads` 在 `AlgorithmsCall` 下添加全图命令；`DVCMFindLightBeads` 为 `IRectangle` 提供矩形右键命令。两者创建临时 `FindLightBeadsConfig`，在属性窗口的 `Submitted` 事件中调用 `FindLightBeads.Execute`，不是打开参数窗口时就检测，也没有在此保存配置。

发现由 `EditorToolFactory.cs` 扫描其实际程序集集合并构造上下文实例。这两个菜单不实现 `IAlgorithmCatalogBoundMenu`，因此 `IsAlgorithmMenuExecutable` 不对它们执行统一 Runtime 的 provider 检查。菜单存在不证明 DLL 或当前像素格式可执行，也不是所有程序集内的类都会无条件显示。

当前 ImageEditor 工程通过 `ProjectReference` 引用 `UI/ColorVision.Core`，不是旧说明中的固定版本 Core NuGet 引用。实际绑定是 `OpenCVMediaHelper.M_FindLightBeads`，以 Cdecl 调用 `opencv_helper.dll`；导出声明在 `Native/include/opencv_media_export.h`，实现分别在 `Native/opencv_helper/opencv_media_export.cpp` 和 `Native/opencv_helper/algorithm.cpp`，不是旧 `Core/opencv_helper/` 路径。Windows/x64、匹配的 native DLL 和依赖仍是运行前提；首次构建与 ABI/发布边界见 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)。

## 灯珠参数不是网格完整性承诺

参数类位于 `EditorTools/GraphicEditing/GraphicEditingWindow.xaml.cs`，当前新建实例的值如下。属性 setter 的限制不等于字段初值已经经过 setter，也不等于直接 native JSON 调用受到同样校验。

| 参数 | UI 新实例值与编辑限制 | native 或绘制含义 |
| --- | --- | --- |
| `Threshold` | `20`；setter 限制到 `0..255` | 转为 Gray8 后使用 `THRESH_BINARY` |
| `MinSize` | `2`；setter 最小为 `1` | 亮轮廓包围框的宽、高均须严格大于下限；直接 native 输入非正数时，亮点分支可忽略下限 |
| `MaxSize` | 初值 `-1`；setter 最小为 `1` | 亮点分支在非正数时不限制上限，否则宽、高均须严格小于上限；暗区分支的尺寸条件不同 |
| `Rows` / `Cols` | 初值均为 `-1`；各 setter 最小为 `1` | 参与预期数量乘积；两者都大于零时还用于大暗区网格步长，不对亮点做完整行列配准 |
| `Radius` | `20`；setter 没有范围限制 | 仅用于 UI 圆标注；native 不读取该字段，不是 native 的检测尺寸阈值 |

直接调用导出且 JSON 缺少字段时，native 的回退值为 `Threshold=20`、`MinSize=2`、`MaxSize=20`、`Rows=650`、`Cols=850`。UI 会序列化自身配置，不能把这些缺字段回退值当成 UI 默认值。这里没有 `MinSize < MaxSize` 或正行列数的统一业务校验。

## 灯珠输入、ROI 与像素布局

`Execute` 获取当前 `ImageFrameLease`，没有帧时直接返回。实际 `ImageView.AcquireImageFrameCore` 经帧存储取得租约；创建帧时优先复制 `ViewBitmapSource`，其次 `ImageShow.Source`，且要求是 `WriteableBitmap`。这不是重新读取磁盘源文件，也不保证取得未转换的原始 TIFF 样本。它没有经过统一算法输入工厂的格式语义归一化。

- 矩形入口在生成右键菜单时，将矩形乘配置中的 `DpiX/DpiY ÷ 96` 并四舍五入到像素坐标，再与当时帧的图像边界求交；宽高非正或无交集时不添加菜单。保存下来的 ROI 到参数提交时不会再按新帧重新裁剪。
- native 只在 ROI 宽高为正且完整位于图内时裁剪；空 ROI、部分越界和完全越界都回退到全图，而不是自动求交或报 ROI 错误。全图菜单传默认零 ROI。
- native 坐标相对于实际使用的工作图。UI 无条件加上传入的 `roiRect.X/Y`；若调用者传越界 ROI 而 native 回退全图，标注仍会加偏移。不能把右键入口的求交推断为导出 API 的安全边界。

`HImage` 只有行列、通道数、位深、stride 和指针，不表达 RGB/BGR、调色板或预乘 Alpha。`HImageToMatView` 检查描述符和行跨度，并按给定 stride 创建 Mat 视图，不做颜色转换，也无法凭描述符证明外部指针实际拥有足够内存。

灯珠实现先将 16U 乘 `255/65535` 转为 8U；其他非 8U 深度直接 `convertTo`，没有把 `[0,1]` 浮点范围乘到 `0..255`。三、四通道按 `COLOR_BGR2GRAY` 转灰度，单通道直接使用；不能从通用 HImage 可表达更多通道推断灯珠算法支持它们。

托管 `ToHImage` 是按格式映射后复制像素，并未交换 `Rgb24`/`Rgb48` 的红蓝通道、展开 `Indexed8` 调色板或反预乘 `Pbgra32`。因此这些布局不能套用统一算法平台的规范化保证；浮点量程、RGB 顺序、调色板和 Alpha 对检测的影响需要单独验证。Gray8/Gray16 和真实 BGR 输入也仍需样本精度验证，而不是依据转换代码宣称检测准确。

## 灯珠 JSON 结果与已知不完整性

亮点来自阈值图经 `2×2` 椭圆腐蚀、`4×4` 椭圆膨胀、`2×2` 椭圆腐蚀后的外轮廓。符合尺寸条件时，以整数包围框中心填入 `Centers`；不是强度质心或亚像素中心。

| 输出 | 当前含义 |
| --- | --- |
| `Centers` / `CenterCount` | 合格亮轮廓的包围框中心及数组长度；没有行列排序或逐格匹配保证 |
| `BlackCenters` / `BlackCenterCount` | 暗区启发式候选点及数组长度，不是完整、逐格核验的缺失灯珠清单 |
| `ExpectedCount` | `size_t(Rows) * size_t(Cols)`；负数没有先被拒绝，不能将非正行列数产生的无符号运算结果视为有效预期数量 |
| `MissingCount` | `ExpectedCount > CenterCount ? ExpectedCount - CenterCount : 0`，不是 `BlackCenterCount`，也不是实测缺失数 |

暗区分支先取已检亮点的凸包，将凸包外阈值图设为白色，再做 `12×12` 矩形膨胀、反相和外轮廓检测。若没有凸包，外部区域屏蔽步骤不执行。符合严格尺寸条件的暗轮廓产生一个包围框中心；不符合时，只有凸包非空且 `Rows/Cols` 都为正才按凸包包围框宽/`Cols`、高/`Rows` 生成网格候选，起点偏移为 `4`，并用 `pointPolygonTest` 过滤。此处不是拿 `Centers` 与一套已配准的完整灯珠网格逐一相减。

重要的当前实现限制：`algorithm.cpp` 在遍历暗轮廓的循环体末尾就 `return 0`，因此只处理第一条暗轮廓，之后的暗轮廓被跳过。即使 native 成功返回 JSON，也不能承诺 `BlackCenters` 覆盖所有暗区。这个源码可见限制不等于已经用真机样本复现，也不应在文档维护时悄悄修产品算法。

## 灯珠完成、叠加与失败边界

`Execute` 返回 `void`，内部丢弃 `Task.Run` 的任务。后台调用 `M_FindLightBeads` 本身是同步的，返回正数表示 JSON 字符串分配成功，数值是含结束符的缓冲区长度，不是灯珠数量，也不是异步任务 ID。调用 `Execute` 返回不能作为保存完整标注的完成信号；当前入口不公开完成事件或可等待任务。

native 在调用期间借用帧，托管 `using (lease)` 在调用结束或异常时释放租约。成功字符串经 `PtrToStringAnsiAndFree` 读取并调用 `FreeResult` 释放，然后才调度 UI 发布。UI 发布前仅检查当前图像 revision：过期结果不绘制；同一 revision 多次执行没有统一 Runner 的 invocation/token 排他与取消机制，仍有效的结果会分别追加，不能宣称 latest-wins。

UI 从 native JSON 的 `Centers` 画红色 `DVCircle`，从 `BlackCenters` 画黄色 `DVCircle`，半径均取 `Radius`，线宽按缩放调整并对无效缩放回退。每个圆通过 `DrawCanvas.AddVisualCommand` 加入可撤销绘图；不是旧说明的蓝圆、红矩形。统计对话框代码已注释，虽然读取 `CenterCount`/`BlackCenterCount`，当前不显示这些统计，也不读取 `ExpectedCount`/`MissingCount` 来显示“检测完成统计”。

这条链不创建中立 algorithm artifact，不调用 `AlgorithmOverlayManager`，不在执行前清理既有标注，也不提交源像素或自动保存文件。保存 source 与 rendered 的差别见[图像编辑器输出](../ui-components/ColorVision.ImageEditor.md)，不能把画布上的圆等同于源图已改写或业务结果已落库。

导出先将输出指针置空。无效图像/空参数返回 `-1`，内部算法失败为 `-2`，分配失败为 `-3`，JSON 格式/类型异常为 `-4`，OpenCV、标准和其他 native 异常分别映射为 `-5/-6/-7`。UI 对非正返回值在 revision 仍有效时显示错误框；它没有包围整个托管任务的异常处理，不能承诺缺少 DLL、入口点错误或托管 JSON 解析异常也会进入这个错误框。

## P2：Ghost、旋转模板和双目调试

`EditorTools/Algorithms/Calculate/P2/` 是同类直接 native 适配器，但不是灯珠算法的另一组参数。三个全图菜单都挂在 `AlgorithmsCall` 下，没有实现 `IAlgorithmCatalogBoundMenu`；它们不自动继承统一 Runtime 的实验 provider 门禁。实际绑定的三个导出实现位于 `Native/opencv_helper/exports/p2_export.cpp`。

| 工具与源码 | 当前输入及执行链 | 结果边界 |
| --- | --- | --- |
| `GhostLocalAnalysis`，`GhostAnalysis.cs` | 当前图像或矩形 ROI → `P2JsonAnalysisWindow` → `M_DetectGhosts` | 原始 JSON、亮源/候选计数、严重度/置信度摘要与 overlay；不调用 `TemplateGhost` 或 MQTT，也不读 Ghost DAO |
| `RotatedTemplateLocalAnalysis`，`RotatedTemplateAnalysis.cs` | 先在矩形右键“设为旋转匹配模板”，再对当前图像/ROI 调用 `M_MatchRotatedTemplate` | 匹配 JSON 与角度/位置等叠加；模板是当前 `DrawEditorContext` 会话持有的位图，不是 Engine 模板数据库记录 |
| `CMStereoBinocularLocalAnalysis`，`StereoFusionAnalysis.cs` / `StereoFusionDebugWindow.xaml.cs` | 当前编辑器图像作左图，选择文件作右图，带标定 JSON 调用 `M_CalStereoBinocularFusion` | 左右五点、三维毫米坐标、视差、重投影误差、置信度及有效状态；不是单图 `M_CalBinocularFusion` |

P2 矩形入口使用 `P2RoiHelper.TryFromRectangle` 转换 DPI 并与当时图像求交；`Normalize` 只把非正尺寸 ROI 替换为全图，不提供任意 ROI 的通用边界校验。全图双目调试始终向导出传左右完整图像范围。不要把前面的 FindLightBeads 越界回退规则直接推广给全部 P2 内核。

### 参数、模板和标定的真实来源

Ghost 和旋转模板窗口允许编辑 JSON 对象，默认参数分别由各适配器的 `CreateDefaultConfig` 生成。Ghost 默认不启用曝光归一化、背景核和方向置信度增强，多尺度层数为 1；native 存在增强实现不等于 UI 默认开启全部增强。完整字段继续核对适配器、`p2_export.cpp` 的解析及对应 native 内核，不在源码 README 复制第二份参数表。

旋转模板从 `ImageShow.Source` 裁剪，并以冻结位图保存在按 `DrawEditorContext` 区分的 `ConditionalWeakTable` 会话中。因此模板可能来自当前显示预览，不保证是磁盘原始样本，也不会自动保存为文件。没有模板时打开匹配窗口会提示；运行委托在每次调用时取会话的 `Template`，并非保证窗口打开时那张模板一直冻结不变。每次 native 调用另建并释放模板 `HImage` 快照。

双目窗口创建的焦距 `1000`、零畸变、单位旋转及平移 `[-60,0,0]` 仅为调试示例，**不能用于真实测量**；源码界面也明确提示加载真实标定。真实 `leftCameraMatrix`、`rightCameraMatrix`、畸变、`rotation` 与 `translation` 必须匹配所用相机、图像尺寸和单位。加载含 `calibration` 对象的文件会替换整份配置；加载单独标定对象则只替换当前配置的 `calibration`。未加载外部标定时，重新选择右图会重新生成默认配置，不能假定手工修改始终保留。

右图通过 `P2BitmapLoader` 读取第一帧：列表内的 WPF 像素格式保留，其他格式转为 Bgra32；随后 `P2ImageSnapshot` 用 `ToHImage` 复制。它不等价于统一 Runner 的格式归一化，RGB/调色板/预乘 Alpha 等布局仍应分别核对具体 native 入口，不把 UI 可打开当作测量精度保证。

### 完成、取消与临时叠加

P2 参数窗口只在点击运行时计算，不是滑动预览。适配器重新获取当前帧租约，在 `Task.Run` 中同步调用 native 并等待结果；调用后检查图像 revision。窗口另检查是否已关闭；双目窗口还核对右图是否仍是启动该次运行时的同一对象。它们不使用 `ImageAlgorithmPreviewSession`，不能据此推断 host-wide invocation 的 latest-wins、像素提交或统一 artifact 所有权。

这里的 revision 校验从每次运行开始，不是把窗口绑定到打开时的图像。Ghost/旋转模板的搜索 ROI 在打开窗口时确定，而运行时重新取当前帧；双目左预览和尺寸文案只在构造时赋值，运行却使用新的左图租约。因此运行前换图后，窗口可能仍显示旧预览/ROI/标定而计算新图。应重新打开窗口并核对输入与标定，不能拿旧左预览证明本次计算输入，也不能把“计算期间换图会丢弃”扩大为全窗口生命周期绑定。

`P2NativeJson.Invoke` 要求返回长度为正且指针非空，按 UTF-8 读取对象 JSON，并经 `FreeResult` 释放。这只证明拿到了 JSON，仍须读取 `success`、`statusCode`、`message`、`warnings` 及具体点的 `valid`/质量指标；例如无效常量模板可以返回成功分配但 `success=false` 的 JSON。

关闭窗口只设关闭标志、清理当前记录的叠加并拒绝之后的显示，没有给正在运行的 native 调用传取消令牌，也不等待其停止。不要把关窗当作释放全部 native 资源的即时完成信号；输入租约与快照在后台调用退出后才释放。一个窗口禁用自己的运行按钮也不代表其他窗口或同图调用受到统一排他控制。

P2 overlay 通过 `DrawCanvas.AddOverlayVisual` 添加临时 `DrawingVisual`，不提交 source 像素、不自动保存 JSON/图片，也不是 FindLightBeads 的可撤销 `DVCircle` 命令。Ghost/旋转模板窗口勾选“自动清理”时先移除上一次叠加；关闭该选项反复运行时，`_overlay` 字段只保留最新一项，清理/关闭只移除这项，先前叠加可能残留。此为当前实现限制，不承诺关窗会清空全部历史叠加。双目窗口另清理左右点预览和自己记录的画布叠加，不代表清空整个画布。

双目点预览还有当前代码限制：`ApplyResult` 为 `LeftPointOverlay` / `RightPointOverlay` 设置结果后，紧接着的 `ClearOverlay()` 又清空二者，随后只重建主画布的左图叠加。因此不能承诺成功计算后窗口内的左右点标记保留可见；JSON、点表与主画布叠加是不同显示路径。

## 验证依据与缺口

`AlgorithmCircleOverlayRenderOptimizationTests.cs` 的灯珠用例通过反射直接调用 `AddCircleOverlay`，验证单圆渲染、中心/半径、颜色、缩放线宽和撤销记录，并覆盖无效缩放回退。它不调用 `M_FindLightBeads`，不能证明灯珠检出率、ROI 回退、缺失计数、全部暗区处理、真实 DLL 加载或异步 UI 完成。

尚未在本主题登记灯珠 native 检测结果或端到端菜单的自动化回归。后续获授权验证应区分源码检查与真实运行，重点覆盖正/非正行列数、多个独立暗区、部分越界 ROI、同 revision 并发、换图丢弃、Gray32Float 量程及 RGB/调色板/Alpha 输入；单纯路径和文档构建通过不构成这些行为已验收。本说明不授权启动设备、发布 DLL 或改动用户图像。

`Test/opencv_helper_test/test_p2_algorithms.cpp` 有合成 Ghost、旋转/缩放/遮挡匹配、常量模板拒绝、已知标定五点双目和导出失败清空指针等用例。测试源码存在不代表本次运行过，也不覆盖真实相机标定、完整 WPF 菜单、模板替换、关窗期间 native 执行或关闭自动清理后的叠加残留。这些 UI/交付边界仍需专门验收；不要用统一 Runner 测试替代。
