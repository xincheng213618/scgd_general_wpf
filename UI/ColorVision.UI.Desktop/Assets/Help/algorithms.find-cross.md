## 十字中心与倾角原理 {#algorithm-principles}

本地 FindCross 从图像中的两条十字轴线计算中心、旋转角和光学倾角。生产算法为 `PatternCrossV1`，自动选择亮/暗极性，通过粗搜索及原分辨率拟合检查四臂证据；多个有效十字、截断或证据不足时可拒绝。位置单位为 **px**，旋转和倾角单位为 **°**。检测成功仍需按产品样本评估位置和角度精度。

<figure class="algorithm-figure" style="overflow-x:auto">
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 330" role="img" aria-labelledby="cross-principle-title cross-principle-desc" style="display:block;min-width:720px;width:100%;height:auto;color:#1f2937">
    <title id="cross-principle-title">十字中心与标准中心的偏移换算为倾角</title>
    <desc id="cross-principle-desc">左图在原图内的搜索区域拟合十字四臂，交点 C 与全图标准中心 S 的偏移为 delta x、delta y，ROI 不移动标准中心。右图将像素偏移乘像元尺寸得到传感器上的毫米偏移，再以有效焦距换算反正切角度。</desc>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="17"><text x="25" y="29">定位：四臂支持两轴交点</text><text x="515" y="29">倾角：像素偏移 → 传感器偏移 → 角度</text></g>
    <rect x="25" y="48" width="385" height="223" fill="#f8fafc" stroke="#94a3b8" />
    <rect x="200" y="72" width="185" height="172" fill="#e8f2ff" stroke="#64748b" stroke-dasharray="6 4" />
    <g stroke="#2175d9" stroke-width="10" stroke-linecap="round"><path d="M225 175 L359 195 M300 131.4 L284 238.6" /></g>
    <g fill="none" stroke="#dc8b19" stroke-width="2"><path d="M217.5 159.5 L292 159.5 L292 185" /></g>
    <g fill="#1f2937"><circle cx="217.5" cy="159.5" r="4" /><circle cx="292" cy="185" r="4" /></g>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="15"><text x="192" y="149">S</text><text x="304" y="181">C</text><text x="245" y="149">Δx</text><text x="304" y="163">Δy</text><text x="301" y="91">ROI</text><text x="36" y="68">原图</text><text x="26" y="298">S 默认取全图中心；C 为未取整的校正后中心</text></g>
    <path d="M540 221 L805 221 L805 91 Z" fill="#e8f2ff" stroke="#2175d9" stroke-width="2" />
    <path d="M578 221 A38 38 0 0 0 574.1 204.3" fill="none" stroke="#dc8b19" stroke-width="2" />
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="16"><text x="660" y="249">有效焦距 f（mm）</text><text x="815" y="145">d</text><text x="596" y="212">θ</text><text x="520" y="78">d = Δx × 像元尺寸 / 1000（mm）</text><text x="532" y="298">示意 θ 为 X 倾角；Y 向下偏移时倾角取负</text></g>
  </svg>
  <figcaption>图中偏移仅作示意；实际倾角使用畸变校正和中心补偿之后、兼容中心取整之前的中心坐标。</figcaption>
</figure>

### 工作步骤 {#cross-steps}

1. 从整图或指定 ROI 搜索唯一十字，自动判断亮/暗极性，并限制在预期角度及允许偏差内。
2. 在原始分辨率拟合两条轴线，检查四臂长度、覆盖、内点和正交关系；用两轴交点定位中心。
3. 通过双轴 `RobustTwoAxis` 估计旋转；它结合两轴方向、正交关系和名义方向，不是单条边的斜率。
4. 按需执行 Brown 镜头畸变校正，再对中心叠加 `CalibrationOffset`，换算 X/Y 倾角。
5. 检查成功标记和单结果项；Flow 还需提交结果文件、数据库和有效下游引用。

## 在图像中运行

前提是当前图像已加载，且进程能加载匹配的 `opencv_helper.dll` 及 OpenCV 依赖。菜单可见不代表 DLL 已可用。

1. 在图像右键菜单选择“分析测量 → 定位与几何 → 十字中心与倾角...”执行整图检测；需要限制范围时，先绘制矩形，再从该矩形的右键菜单进入同名命令。
2. 在“十字中心与倾角”参数窗口中设置预期角度、最大允许旋转偏差及光学校准。默认以整幅图像中心作为倾角基准；只有关闭“标准中心使用图像中心”后，才使用填写的标准中心 X/Y。
3. 提交参数后开始后台计算。默认在图像上显示轴线、中心、角度、倾角和诊断摘要；开启“弹窗显示结果”会额外显示数值窗口。失败时显示原因。

参数按当前 `ImageProcessingContext` 暂存并在同一上下文的整图/矩形入口共享，此处没有配置落盘操作。输入使用当前图像帧租约。矩形先按 DPI 换算到像素，左上向下取整、右下向上取整，再与图像求交；运行时会按实际帧再次规范化 ROI。Flow 和直接 ABI 的 ROI 规则见下文，不能照搬这个自动求交行为。

每次有效提交会清除同标签的旧叠加，并记录请求序号。图像版本已变化或同标签已有更新请求时，旧结果不再回显；这不等于取消了已经开始的 native 计算。该入口返回 `void`，没有供调用者等待的公开完成任务。它也不将本次结果写入 Engine 历史结果表。

## 生产参数

下表是 `FindCrossLocalOptions` 和 Flow“算法参数”接受的生产字段。可从以下最小 JSON 开始；缺省字段使用托管类型默认值：

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

本地节点的“算法参数”显示角度摘要，通过“编辑…”打开独立配置窗口，不加载或保存 JSON 算法模板。默认使用中文表单，包含定位、光学、自动/标定标准中心；高级区域提供中心补偿和镜头畸变。可切换 JSON；语法错误或未知字段保留原文本修正，不静默丢弃。表单与执行共用生产参数契约，应用前检查范围和完整内参；取消不回写。搜索区域关注点与手动搜索区域属于同一组，选择关注点时隐藏手动区域，清空后恢复且保留旧值。节点仍使用原 `ParameterJson` 字段保存，未修改时原文保留，编辑其他字段不丢弃已保存的校准数据。

## 图像、ROI 与结果坐标

`HImage` 描述符和 stride 必须与实际像素布局一致，调用期间必须保持缓冲区有效。算法预处理对三通道使用 BGR、四通道使用 BGRA 灰度转换；描述符不包含颜色顺序、调色板或预乘 Alpha 语义，不能由通道数推断所有图像布局都等价。

| 边界 | 规则 |
| --- | --- |
| native `RoiRect` / Flow“搜索区域” | `0,0,0,0` 表示整图；否则宽高必须为正，区域必须完整位于图像内。越界返回参数错误或由 Flow 提前拒绝，不自动扩展为整图 |
| `result[0].x/y/w/h` | 实际搜索 ROI 的全图位置和宽高，不是十字外接框 |
| `result[0].center` / 托管 `Items[0].Center` | 全图像素坐标，经畸变校正及 `CalibrationOffset` 后用 `std::lround` 取整；托管属性虽为 `double`，native 提供的这一字段仍是整数 |
| `diagnostics.CenterSubpixel` | 未取整的输出中心。需要亚像素数据时读取此项，不从整数 `center` 推测精度 |
| `diagnostics.RawGeometricCenter` / `RawArmEndpoints` | 原图中的几何中心/轴端点，供叠图对照 |
| `diagnostics.ArmEndpoints` | 校正后的轴端点；`CalibrationOffset` 只移动输出中心，不平移这些端点 |
| `rotationAngle` | 度；Pattern 模式使用两轴拟合的 `RobustTwoAxis`，不是选择某一条边的角度 |
| `tilt.tilt_x/tilt_y` | 度；用未取整的校正后中心相对标准中心的偏差计算，图像 X 向右为正，Y 向下时 `tilt_y` 取负号 |

### 倾角公式与示例 {#cross-tilt}

令未取整的校正后中心为 $C=(c_x,c_y)$，标准中心为 $S=(s_x,s_y)$，像元尺寸为 $p$（μm），有效焦距为 $f$（mm）：

$$
\theta_x=\arctan\left(\frac{(c_x-s_x)p}{1000f}\right)\frac{180}{\pi},\qquad
\theta_y=-\arctan\left(\frac{(c_y-s_y)p}{1000f}\right)\frac{180}{\pi}
$$

例如中心向右 100 px、向下 50 px，$p=3.76$ μm、$f=25.4$ mm，得到 X 倾角约 **+0.85°**、Y 倾角约 **−0.42°**。这两项是中心相对标准中心的角度偏差，十字在图像内旋转的 `rotationAngle` 是另一项指标；旋转十字但交点不移动时，倾角不会因此增加。

native 在 `std::lround` 之前计算倾角。需要复算时使用 `diagnostics.CenterSubpixel`；兼容整数 `center` 或下游中心标量不能精确重建同一倾角。独立调用 `FindCrossLocal.CalculateTilt` 必须明确提供标准中心，因为它没有图像尺寸可用于自动解析。

托管解析器要求结果框的起点非负、尺寸为正、数值有限，且结果中心在框内（右/下边界不包含）。偏移过大、ROI 边缘附近取整等情况可能让 native 已生成的结果在解析阶段失败；不能把正返回码直接当成可用中心。

## Flow 输入与保存

“本地十字定位”位于“本地算法”（`Flow_LocalAlgorithms`），节点标识 `LocalFindCross`。连接上游相机取图、校正或“加载图片”节点，通过“算法参数”“搜索区域”“搜索区域关注点”和“结果目录”配置检测与保存。

“搜索区域关注点”选择上游寻找发光区回写的 POI 模板。非空时优先于固定“搜索区域”，每次运行直接读取数据库最新明细；留空兼容原固定区域/整图配置。支持单个中心矩形 Rect、左上角矩形 LTRect，以及按 LT、RT、RB、LB 保存的四角点外接矩形，宽高包含两端像素。不做透视变换，中心和光学基准仍使用原图坐标。不存在、形状错误或越界时停止，不回退全图。结果参数记录模板名称及实际搜索矩形。模板不携带帧身份，流程应保证前序定位成功并写入对应图像坐标，再执行本节点；不同并行流程应使用不同模板。

- 优先使用上游当前内存帧；没有内存帧时，读取 IN 图像结果对应的文件。上游文件不存在会报错，不继续用另一个输入掩盖错误。
- 帧必须完成方向变换。RAW 输入走共用借用函数，要求 8/16 位、1/3 通道交错数据；主缓冲区为 CIE 时使用其亮度数据。原生能表达的更多格式不等于该 Flow 输入适配器都接受。
- 成功后保存 FindCross 主结果、一条公共明细及 UTF-8 JSON 文件。结果目录为空时使用 `%LOCALAPPDATA%\ColorVision\Results\FindCross`；文件名带时间和 GUID。历史底图需要上游保存与实际帧一致的文件，节点不把已校正/翻转的内存结果自动关联到未经处理的源图。
- 下游 `action.Data` 可读取 `LocalFindCrossResult`、`LocalFindCrossCenterX/Y`、`LocalFindCrossRotationAngle`、`LocalFindCrossTiltX/Y`、`LocalFindCrossDiagnostics`、`LocalFindCrossResultFile`；有内容时还保存 `LocalFindCrossRawJson` 和 `LocalFindCrossInteropDiagnostic`。这些中心标量来自兼容结果项，仍是上述取整中心。

检测拒绝或结果校验失败时会尝试保存失败主记录并发布到结果页，随后节点仍失败；不会生成成功明细/JSON，也不把失败主记录作为下游有效 `MasterValue`。输入缺失等前置错误可能更早发生；数据库或发布本身失败时不能假定结果页已有记录。

生产入口输出单十字结果，数据库类型为 `63`、版本为 `1.0`，文件沿用 `result[]`。成功主记录的 `TName` 使用既有 `optCenter` 解析标识，使这组结果进入光轴校准的倾角与旋转结果；失败主记录保持 `LocalFindCross`。参数中的 `Algorithm=LocalFindCross` 保留本地实现身份。`Name` 仅标识 JSON 中的结果项，设置为 `ImageCenter` 也不会增加或切换测量指标；本地十字节点不生成独立的 `ImageCenter` 主记录。

倾角所用的标准中心是配置或整幅图像中心，不是算法独立测得的镜头光轴。要把角度偏差用于光轴校准，需使图卡参考点、标准中心和测量装置标定符合现场测量定义。Flow 画布上未接入执行链的结点不会执行，也不会保存结果。

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

九宫格线条有多个十字交点，整图搜索可能返回 `AmbiguousPattern`。若只需中心交点，配置仅覆盖该交点及足够四臂长度的搜索区域；最终能否稳定检出及角度精度仍需原始相机图像验证。native 正返回码只说明产生 JSON，仍必须检查 `Success` 与 `HasSingleItem`，不能把诊断几何或失败结果交给下游。