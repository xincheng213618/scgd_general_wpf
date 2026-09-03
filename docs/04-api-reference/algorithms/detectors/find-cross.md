---
knowledge_id: "algorithms.find-cross"
knowledge_type: "topic"
status: "current"
summary: "本地十字定位的图像菜单、Flow 节点、生产参数、全图坐标、原生返回值与失败诊断。"
aliases: ["本地十字定位", "本地 FindCross", "M_FindCrossLocal", "M_FindCrossLocalGetLastError", "FindCrossLocal", "FindCrossLocalOptions", "FindCrossLocalResultParser", "LocalFindCrossNode", "PatternCrossV1", "OuterPanel", "ExpectedAngleDegrees", "AngleToleranceDegrees", "CalibrationOffset", "RawGeometricCenter", "LocalFindCrossCenterX", "NativeConfigurationInvalid", "AmbiguousPattern", "PatternClipped", "LegacyParametersIgnored", "标准中心使用图像中心", "最大允许旋转偏差"]
code_paths: ["Native/include/opencv_media_export.h", "Native/opencv_helper/algorithm/find_cross", "UI/ColorVision.Core/FindCrossLocal.cs", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindCross/FindCrossLocalCM.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindCrossNode.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs"]
test_paths: ["Test/opencv_helper_test/test_find_cross.cpp", "Test/ColorVision.UI.Tests/FindCrossLocalTests.cs", "Test/ColorVision.UI.Tests/LocalFindCrossNodeTests.cs", "Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs"]
related: ["algorithms.index", "engine.native-integration", "engine.results", "ui.image-editor", "delivery.native-testing"]
---

# 本地十字定位 FindCross

本地 FindCross 从图像中的两条十字轴线计算中心、旋转角和光学倾角。默认算法为 `PatternCrossV1`，自动选择亮/暗极性，通过粗搜索及原分辨率拟合检查四臂证据；多个有效十字、截断或证据不足时可拒绝。检测成功仍需按产品样本评估位置和角度精度。

ImageEditor 的“本地 FindCross...”和 Flow 的“本地十字定位”共用 `ColorVision.Core.FindCrossLocal` → `M_FindCrossLocal`。图像菜单直接显示结果；Flow 还负责结果文件、数据库和下游输出。远端 `TemplateFindCross` 的模板/MQTT 接入见 [ARVR 模板](../templates/arvr-template.md)，参数不能直接互换。

## 在图像中运行

前提是当前图像已加载，且进程能加载匹配的 `opencv_helper.dll` 及 OpenCV 依赖；部署和 ABI 前提见 [native 集成](../../../02-developer-guide/engine-development/opencv-integration.md)。菜单可见不代表 DLL 已可用。

1. 在图像的算法调用菜单选择“本地 FindCross...”执行整图检测；需要限制范围时，先绘制矩形，再从该矩形的右键菜单进入同名命令。
2. 在“本地 FindCross 参数”中设置预期角度、最大允许旋转偏差及光学校准。默认以整幅图像中心作为倾角基准；只有关闭“标准中心使用图像中心”后，才使用填写的标准中心 X/Y。
3. 提交参数后开始后台计算。默认在图像上显示轴线、中心、角度、倾角和诊断摘要；开启“弹窗显示结果”会额外显示数值窗口。失败时显示原因。

参数按当前 `ImageProcessingContext` 暂存并在同一上下文的整图/矩形入口共享，此处没有配置落盘操作。输入使用当前图像帧租约。矩形先按 DPI 换算到像素，左上向下取整、右下向上取整，再与图像求交；运行时会按实际帧再次规范化 ROI。Flow 和直接 ABI 的 ROI 规则见下文，不能照搬这个自动求交行为。

每次有效提交会清除同标签的旧叠加，并记录请求序号。图像版本已变化或同标签已有更新请求时，旧结果不再回显；这不等于取消了已经开始的 native 计算。该入口返回 `void`，没有供调用者等待的公开完成任务。它也不将本次结果写入 Engine 历史结果表。

## 生产参数

下表是 `FindCrossLocalOptions` 和 Flow“算法参数(JSON)”接受的生产字段。可从以下最小 JSON 开始；缺省字段使用托管类型默认值：

```json
{
  "ExpectedAngleDegrees": 0,
  "AngleToleranceDegrees": 10,
  "opticsParams": {
    "focusLength": 25.4,
    "sensorPixSize": 3.76
  }
}
```

| 字段 | 默认值、单位与约束 |
| --- | --- |
| `ExpectedAngleDegrees` | `0°`，有限值 `[-180,180]`；产品名义方向，用于限定搜索并处理十字的方向歧义 |
| `AngleToleranceDegrees` | `10°`，有限值 `(0,45]`；相对名义方向的允许搜索偏差 |
| `Name` | `Point_1`，不得为空白；标识结果项 |
| `opticsParams.stdCenter` | 省略时使用整幅输入图像的 `(width/2,height/2)`，不是 ROI 中心；显式值为全图像素坐标 `{ "x": ..., "y": ... }` |
| `opticsParams.focusLength` | `25.4 mm`，有限正数 |
| `opticsParams.sensorPixSize` | `3.76 μm`，有限正数 |
| `CalibrationOffset` | 可省略，默认零偏移；有限像素坐标 `{ "x": ..., "y": ... }`，叠加到检测中心后再计算倾角。图像参数窗口不提供此项，托管/API 和 Flow 可传入 |
| `opticsParams.distortion.Enabled` | 默认关闭；启用镜头畸变校正 |
| `opticsParams.distortion.K1/K2/P1/P2/K3` | Brown 畸变系数，默认 `0`，必须有限；应填写实际相机标定值 |
| `opticsParams.distortion.Fx/Fy/Cx/Cy` | 标定内参，单位 px；启用畸变时必须完整提供，`Fx/Fy` 为有限正数，`Cx/Cy` 有限。即使关闭畸变，提供内参时也必须四项完整 |

图像参数窗口在关闭自动标准中心时，X/Y 初值为 `4784/3190`；这只是配置初值，不是任意图像都适用的标定。镜头主点 `Cx/Cy` 与倾角标准中心 `stdCenter` 是不同数据，启用畸变时不能用名义焦距或标准中心自动代替完整相机内参。

生产选项不暴露极性、检测阈值、臂长、可信度、处理分辨率或旋转估计器。Flow 使用不区分属性大小写、拒绝未知成员的反序列化，再执行生产参数校验；把旧服务模板整体复制过来，或加入 `DetectionMode`、`MinConfidence` 等诊断字段，会失败。

## 图像、ROI 与结果坐标

`HImage`、stride、像素缓冲区所有权遵守 [API 参考](../../engine-components/opencv-helper-api.md#himage)。调用期间必须保持缓冲区有效。算法预处理对三通道使用 BGR、四通道使用 BGRA 灰度转换；描述符不包含颜色顺序、调色板或预乘 Alpha 语义，不能由通道数推断所有图像布局都等价。

| 边界 | 规则 |
| --- | --- |
| native `RoiRect` / Flow“搜索区域” | `0,0,0,0` 表示整图；否则宽高必须为正，区域必须完整位于图像内。越界返回参数错误或由 Flow 提前拒绝，不自动扩展为整图 |
| `result[0].x/y/w/h` | 实际搜索 ROI 的全图位置和宽高，不是十字外接框 |
| `result[0].center` / 托管 `Items[0].Center` | 全图像素坐标，经畸变校正及 `CalibrationOffset` 后用 `std::lround` 取整；托管属性虽为 `double`，native 提供的这一字段仍是整数 |
| `diagnostics.CenterSubpixel` | 未取整的输出中心。需要亚像素数据时读取此项，不从整数 `center` 推测精度 |
| `diagnostics.RawGeometricCenter` / `RawArmEndpoints` | 原图中的几何中心/轴端点，供叠图对照 |
| `diagnostics.ArmEndpoints` | 校正后的轴端点；`CalibrationOffset` 只移动输出中心，不平移这些端点 |
| `rotationAngle` | 度；Pattern 模式使用两轴拟合的 `RobustTwoAxis`，不是选择某一条边的角度 |
| `tilt.tilt_x/tilt_y` | 度；用输出中心相对标准中心的偏差计算，图像 X 向右为正，Y 向下时 `tilt_y` 取负号 |

倾角公式为 `atan((centerX-standardX) × sensorPixSize/1000 ÷ focusLength)` 转为度，Y 分量相同计算后取负。独立调用 `FindCrossLocal.CalculateTilt` 必须明确提供标准中心，因为它没有图像尺寸可用于自动解析。

托管解析器要求结果框的起点非负、尺寸为正、数值有限，且结果中心在框内（右/下边界不包含）。偏移过大、ROI 边缘附近取整等情况可能让 native 已生成的结果在解析阶段失败；不能把正返回码直接当成可用中心。

## Flow 输入与保存

“本地十字定位”位于 `Flow_CustomNodes`，节点标识 `LocalFindCross`。设置“图像文件”“算法参数(JSON)”“搜索区域”和“结果目录”即可定义输入和保存位置。

- 优先使用上游当前内存帧；没有内存帧时，使用配置的图像文件，再查 IN 图像结果的文件。已配置文件不存在会报错，不继续用另一个输入掩盖错误。
- 帧必须完成方向变换。RAW 输入走共用借用函数，要求 8/16 位、1/3 通道交错数据；主缓冲区为 CIE 时使用其亮度数据。原生能表达的更多格式不等于该 Flow 输入适配器都接受。
- 成功后保存 FindCross 主结果、一条公共明细及 UTF-8 JSON 文件。结果目录为空时使用 `%LOCALAPPDATA%\ColorVision\Results\FindCross`；文件名带时间和 GUID。历史底图需要上游保存与实际帧一致的文件，节点不把已校正/翻转的内存结果自动关联到未经处理的源图。
- 下游 `action.Data` 可读取 `LocalFindCrossResult`、`LocalFindCrossCenterX/Y`、`LocalFindCrossRotationAngle`、`LocalFindCrossTiltX/Y`、`LocalFindCrossDiagnostics`、`LocalFindCrossResultFile`；有内容时还保存 `LocalFindCrossRawJson` 和 `LocalFindCrossInteropDiagnostic`。这些中心标量来自兼容结果项，仍是上述取整中心。

检测拒绝或结果校验失败时会保存失败主记录并发布到结果页，随后节点仍失败；不会生成成功明细/JSON，也不把失败主记录作为下游有效 `MasterValue`。输入缺失等前置错误可能更早发生。数据库、发布与展示的完整责任见 [Engine 结果链](../../engine-components/result-handoff-chain.md)。

## 原生返回值与托管调用

```cpp
int M_FindCrossLocal(HImage image, RoiRect roi, const char* configJson, char** resultJson);
int M_FindCrossLocalGetLastError(char* buffer, std::uint32_t bufferLength);
```

托管绑定使用 Cdecl 与 UTF-8。优先调用 `FindCrossLocal.Run(image, roi, options)`；需要兼容旧 JSON 或诊断模式时用 `RunJson`。后者先检查 JSON 是非空对象，字段语义仍由 native 解析。`Run` 的无效生产参数会返回 `InvalidConfiguration`；传入空 `options` 则抛 `ArgumentNullException`。

| 返回值/失败类型 | 当前含义与处理 |
| --- | --- |
| `> 0` | UTF-8 JSON 缓冲区字节数，包含末尾 NUL；调用者用 `FreeResult` 释放，并继续检查 JSON 的 `Success` |
| `-1` / `NativeInvalidArgument` | 输入/输出指针、图像或 ROI 参数无效 |
| `-3` / `NativeAllocationFailed` | 结果缓冲区分配失败或超过长度上限 |
| `-4` / `NativeConfigurationInvalid` | native JSON 解析/字段校验失败；立即在同一线程读取 last-error 获取具体字段 |
| `-5` / `NativeOpenCvError` | OpenCV 异常 |
| `-6/-7` / `NativeProcessingFailed` | 标准异常/未知异常 |
| `NativeLibraryUnavailable` / `NativeEntryPointUnavailable` | 检查实际加载的 DLL、依赖及导出版本 |
| `NativeLibraryIncompatible` / `NativeAbiMismatch` | 检查位数、调用约定和结构布局 |
| `ResultParseFailed` | JSON 缺字段、非有限值、结果中心越界，或成功状态与结果数组矛盾；保留 `RawJson` 和 `InteropDiagnostic` 排查 |
| `NativeResultReleaseFailed` / `NativeFreeEntryPointUnavailable` | 释放失败；托管层会将原结果变为失败并清空有效结果项 |

`M_FindCrossLocalGetLastError` 返回所需字节数（含 NUL）；传空指针或缓冲区不足时不写入，仍返回所需长度。错误按线程保存，每次检测开始清空，空错误长度为 `1`；并非每个 `-1` 都有详细文本。旧 DLL 缺少这个可选诊断导出时，托管层保留原始调用错误，不用诊断读取失败覆盖它。

当前 native 成功结果含一条 `result`；算法拒绝返回 `Success=false`、空 `result` 和 `FailureReason`，仍可能是正返回码。托管解析器接受没有 diagnostics 的旧结果信封，也校验根级和诊断级成功状态的一致性。调用者应检查 `Success`/`HasSingleItem`，不能只看 `NativeReturnCode` 或解析函数的布尔值。`Run/RunJson` 在 `finally` 释放非空结果指针，调用方不再释放托管结果。

## 诊断模式与旧参数

通过 `RunJson`/原生 JSON 可显式选择 `DetectionMode="OuterPanel"`，其结果标识为 `OuterPanelAssistV2`：它利用外轮廓推断中心，并不证明找到了显示的十字。`PatternCross` 是默认模式；Pattern 模式的中心固定为轴线交点，旋转固定为双轴估计，兼容的 `CenterMethod`/`RotationMethod` 不会替换这两个生产策略。

原生还接受 `PatternPolarity`（`Auto/Bright/Dark`）、`MinPatternContrast`、`MinArmLengthPixels`、`MinArmCoverage` 以及亮区解析器的诊断选项。有效字段、范围和优先级由 `ParseFindCrossConfig` / `ParsePatternConfig` 管理；这些不属于 Flow 的生产选项。兼容处理包括：

- 没有 `AngleToleranceDegrees` 时读取 `CheckLine.floAngle`；没有 `MinArmLengthPixels` 时用旧 `minLineLength/2`。
- `CenterOffsetX/Y` 覆盖 `CalibrationOffset` 对应分量。
- `threshold`、`blurKernel`、`maxLineGap`、`caclWay`、`debugCfg` 等旧字段被忽略；列入 `IgnoredParameters` 并产生 `LegacyParametersIgnored`。未知字段一般被忽略，但不保证所有未知字段都列入该清单。
- `debugCfg.Debug=true` 不写调试文件，诊断随 JSON 返回；`LegacyCompatible` 别名产生 `CompatibilityAliasNotVendorEquivalent`，不承诺与供应商旧算法数值一致。

原始 `diagnostics` 还含 `ArmQuality`、请求/实际模式及完整有效光学信息；托管 diagnostics 只暴露其中部分字段，其他信息从 `RawJson` 查询。

## 按失败原因排查

| 原因 | 优先检查 |
| --- | --- |
| `NoSignal` / `LowPatternContrast` | 灰度动态范围、量程、照明和十字对比度 |
| `NoPatternCandidate` | ROI 是否包含目标、预期角度和搜索偏差是否覆盖它 |
| `AmbiguousPattern` | 搜索范围内是否有两个均通过质量检查的空间候选；用目标区域消除歧义 |
| `PatternClipped` / `InsufficientArmSupport` | 四臂是否被 ROI、图像边缘或遮挡截断 |
| `InsufficientFullResolutionInliers` / `PoorLineFit` / `PoorPatternSharpness` | 原分辨率的轴线样本、离群点和清晰度 |
| `NonOrthogonalAxes` / `UnstableRefinement` | 十字形状、两轴关系和粗定位到精定位的一致性 |
| `InvalidDistortionGeometry` / `InvalidCenterGeometry` | 标定内参、畸变系数、ROI 与坐标空间；不要把诊断几何直接当成功输出 |
| `LowConfidence` | 综合质量不足；查看 `Confidence`、`ArmQuality` 和 `Warnings`，结合实际样本处理 |

## 验证入口与边界

`FindCrossLocalTests` 覆盖生产参数、兼容 JSON、诊断解析、调用约定及释放异常；其中 `NativeV2Fact` 用例依赖实际 native 环境，不能把普通托管用例等同于导出已执行。`LocalFindCrossNodeTests` 使用替身验证输入选择、生产 JSON、结果校验、事务与失败发布；`FindCrossResultOverlayTests` 检查历史结果绘制契约。

原生合成样本和 `.cvraw` 标注对照入口见[原生测试与调试](../../../02-developer-guide/engine-development/native-testing.md)。检查实际退出码和跳过信息；合成测试、页面构建或正确叠图均不能代替现场图像的精度、重复性及误检验收。
