---
knowledge_id: "algorithms.matching"
knowledge_type: "topic"
status: "current"
summary: "说明本地模板匹配的节点内置配置、cvtm 导入导出与无数据库执行，以及旧服务 Matching 的请求与 AOI 结果。"
aliases: ["模板匹配用错模板怎么查","AlgorithmTMNode","ViewHandleMatching","本地模板匹配","LocalTemplateMatchingNode","cvtm","TemplateMatchingWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/Matching/TemplateMatch.cs", "Engine/ColorVision.Engine/Templates/Matching/ViewHandleMatching.cs", "Engine/ColorVision.Engine/Templates/Matching/AlgResultAoiDao.cs", "Engine/FlowEngineLib/Node/Algorithm/AlgorithmTMNode.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalTemplateMatchingNode.cs", "Engine/ColorVision.Engine/Services/Algorithms/LocalTemplateMatchingService.cs", "Engine/ColorVision.Engine/PropertyEditor/LocalMatchingTemplatePropertiesEditor.cs", "UI/ColorVision.Core/TemplateMatching.cs", "UI/ColorVision.Core/TemplateMatchingFile.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/P2/TemplateMatchingWindow.xaml.cs", "Native/opencv_helper/algorithm/matching/rotated_template_matching.cpp", "Native/opencv_helper/algorithm/matching/rotated_template_matching.h"]
test_paths: ["Test/ColorVision.UI.Tests/LocalTemplateMatchingTests.cs","Test/opencv_helper_test/test_p2_algorithms.cpp"]
related: ["algorithms.index","algorithms.template-primitives","engine.results"]
---

# Matching 模板匹配

节点库“本地算法”中的 **模板匹配** 使用本机算法，用参考图在输入图像中搜索相似目标，支持旋转、尺度、多目标、分数阈值和距离抑制。它与依赖亮暗边界的发光区定位用途不同，仍需具有可区分结构的参考图。旧服务流程节点 `AlgorithmTMNode` 向服务发送 `Event_MatchTemplate`，仅用于兼容已有流程，不出现在新增节点菜单；两种模板格式不能互换，也不会自动迁移。业务使用的 [AA 发光区定位](./find-light-area.md) 走独立的 `FindLightArea` 请求链。

<!-- algorithm-help:start -->

## 模板匹配算法原理 {#algorithm-principles}

本地模板匹配把一张包含可区分结构的参考图，按配置枚举角度和尺度，再在待测图的搜索区域中寻找相关峰。它适合目标位置变化、允许范围内的旋转和大小变化；参考图过于平坦、重复纹理过多或目标超出搜索范围时，单纯调低分数阈值不能保证找到正确目标。

<figure class="algorithm-figure">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 660 270" role="img" aria-labelledby="matching-principle-title matching-principle-desc">
  <title id="matching-principle-title">参考图经过旋转和缩放后在搜索区域中定位</title>
  <desc id="matching-principle-desc">左边是一张含 L 形纹理的参考图。右边展示搜索区域中旋转后的目标，标出中心和按参考图顺序变换后的四个角点。输出恢复为原图像素坐标。</desc>
  <defs><marker id="matching-principle-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 Z" fill="#64748b" /></marker></defs>
  <g font-family="Segoe UI, Microsoft YaHei, sans-serif" font-size="16" fill="#1f2937">
    <text x="84" y="28" text-anchor="middle">参考图 T</text>
    <rect x="24" y="65" width="120" height="100" rx="4" fill="#f1f5f9" stroke="#64748b" />
    <path d="M54 84 V142 H115" fill="none" stroke="#2563eb" stroke-width="12" />
    <path d="M167 115 H300" fill="none" stroke="#64748b" stroke-width="2" marker-end="url(#matching-principle-arrow)" />
    <text x="233" y="88" text-anchor="middle">旋转 θ 与尺度 s</text>
    <rect x="330" y="45" width="300" height="170" fill="#f8fafc" stroke="#64748b" stroke-dasharray="6 4" />
    <text x="480" y="28" text-anchor="middle">待测图的搜索区域</text>
    <polygon points="398,98 520,66 550,176 428,208" fill="#dbeafe" stroke="#2563eb" stroke-width="2" />
    <path d="M431 111 L448 172 L513 155" fill="none" stroke="#2563eb" stroke-width="12" />
    <circle cx="474" cy="137" r="5" fill="#dc2626" />
    <text x="485" y="132">中心</text>
    <text x="385" y="91">1</text><text x="525" y="61">2</text><text x="556" y="187">3</text><text x="413" y="224">4</text>
    <text x="330" y="252" text-anchor="middle">同一参考图比较多个姿态  输出中心  角度  尺度  分数和四角</text>
  </g>
</svg>
<figcaption>图示为几何关系。四角顺序沿参考图左上、右上、右下、左下变换，坐标统一还原到整图。</figcaption>
</figure>

### 从参考图到匹配结果 {#matching-processing}

1. **固定本次配置与图像。** 节点解析自身保存的参考像素和参数，使用上游本地内存帧，校验搜索 ROI。全零 ROI 表示全图，非空 ROI 必须完整位于图像内。
2. **建立特征。** 参考图与输入图先转为浮点灰度并按自身动态范围归一化。`Intensity` 直接使用归一化灰度；默认 `Gradient` 使用 3×3 Sobel 的梯度幅值，再按最大幅值归一化。常量或动态范围不足的输入会被拒绝。
3. **枚举角度和尺度。** 以配置步长生成姿态模型，在有效掩码内计算归一化互相关。金字塔先粗定位，再在原分辨率附近细化；模型过小或放不进搜索区时会减小有效层数或跳过该姿态，并记录警告。
4. **选峰与重评分。** 先提取候选相关峰；允许遮挡时，再按样本差异选取保留子集重算分数。启用亚像素时，以相邻响应值作局部二次峰值细化。
5. **去重并输出。** 候选按最终分数降序排列，按目标中心距离抑制重复结果，最多保留 `MaxMatches` 个。没有达到门限的匹配时返回 `no_matches`，流程节点按失败处理。

对于位置 $p$、角度 $\theta$ 和尺度 $s$，相关分数的基本形式为：

$$
S(p,\theta,s)=\frac{\sum_{q\in\Omega}F_I(p+q)F_{T_{\theta,s}}(q)}{\sqrt{\sum_{q\in\Omega}F_I(p+q)^2\sum_{q\in\Omega}F_{T_{\theta,s}}(q)^2}}
$$

$F_I$ 与 $F_T$ 是所选灰度或梯度特征，$\Omega$ 是旋转参考图的有效掩码。当前使用 `TM_CCORR_NORMED`，公式不减均值。分数是相关相似度，`0.8` 不表示 80% 的命中概率，也不是坐标误差百分比。

允许遮挡比例为 $\tau>0$ 时，只考虑掩码内且参考特征幅值大于 0.05 的有效样本；有效样本至少需要 16 个。按绝对差异保留约 $(1-\tau)$ 的样本，实际保留数为 $K=\max(16,\lceil(1-\tau)n\rceil)$，再用上述归一化相关公式计算最终分数。`VisibleFraction=K/n` 表示保留样本比例，**不是独立测得的可见面积或真实遮挡比例**；未启用重评分时为 1。

### 关键参数与结果含义 {#matching-metrics}

| 参数 | 新配置默认值与范围 | 作用 |
| --- | --- | --- |
| `ScoreThreshold` | 0.8；0～1 | 最终分数门限，低于门限的候选不输出 |
| `MaxMatches` | 10；1～10000 | 最多返回目标数，不是期望必须找到的数量 |
| `NmsRadius` | 12 px；0～1000000 | 中心距离小于或等于此值时保留较高分候选；设为 0 不扩大距离保护圈，完全重合的中心仍可去重 |
| `AngleMin/Max/Step` | −15°～15°，步长 1° | 只搜索这些离散角度，不能把步长当成最终角度误差保证 |
| `ScaleMin/Max/Step` | 0.9～1.1，步长 0.05；尺度大于 0、最大 20 | 相对参考图大小；固定大小时最小、最大均设为 1 |
| `FeatureMode` | `Gradient`；可选 `Intensity` | 比较梯度幅值或灰度纹理 |
| `OcclusionTolerance` | 0.2；0～0.75 | 允许重评分时忽略较大差异样本，也可能增加误匹配 |
| `PyramidLevels` | 2；1～8 | 粗到细搜索层数；实际层数受参考图尺寸和搜索区限制 |
| `Subpixel` | true | 对响应峰作局部细化，不构成亚像素精度验收结论 |

角度数和尺度数的乘积最多为 10000。例如默认角度有 31 个、尺度有 5 个，共 155 个姿态模型；这不是最终目标数量。角度、尺度范围与步长必须为有效有限值。

| 输出字段 | 含义与单位 |
| --- | --- |
| `Center` | 目标中心的整图 X/Y 像素坐标，X 向右、Y 向下 |
| `AngleDegrees` | 参考图按 OpenCV 图像旋转约定匹配到的平面角度，单位 °，不表示器件空间倾角 |
| `Scale` | 相对参考图的线性尺寸比例，无量纲 |
| `Score` | 通过最终门限的相关分数，0～1，越高表示在当前特征和搜索范围内越相似 |
| `VisibleFraction` | 鲁棒重评分使用的保留样本比例，0～1 |
| `Corners` | 按参考图顺序输出四角，整图像素坐标；跟随所匹配角度和尺度变换 |
| `Warnings`、`StatusCode` | 姿态被跳过、金字塔层数缩减或失败原因；存在 JSON 不等于成功 |

角度遵循图像坐标变换 $R(\theta)=\left[\begin{smallmatrix}\cos\theta&\sin\theta\\-\sin\theta&\cos\theta\end{smallmatrix}\right]$。例如参考图宽 40 px、匹配尺度 1.2，则目标对应宽度约 48 px；该宽度随目标一起旋转，不能直接把旋转后外接矩形宽度当成 48 px。分数门限 0.8 时，最终分数 0.83 的候选可保留，0.77 的候选不会成为成功结果。

## 本地模板、参数与使用步骤

本地窗口与 `LocalTemplateMatchingNode` 共用 `TemplateMatching.Run`，直接调用现有 `M_MatchRotatedTemplate`；需要 Windows/x64 的 `opencv_helper.dll` 及依赖。参考图和参数保存在流程节点内，随流程保存和复制；单独分享配置时才导出 `.cvtm`，默认导出位置为当前用户 `%LocalAppData%\ColorVision\Templates\Matching`。匹配执行无需 MySQL、参数模板数据库或外部参考图路径。

1. 在图像编辑器打开输入图，绘制目标矩形，右键“设为旋转匹配模板”；也可在管理窗口“导入参考图”。矩形模板从编辑器源帧复制像素，不从伪彩显示图截取；文件导入使用 WPF 第一帧解码，格式支持边界仍见本地分析说明。
2. 从“分析测量 → 定位与几何 → 旋转模板匹配...”进入窗口，或右键搜索矩形“在此 ROI 执行旋转模板匹配”。“导入模板”读取 `.cvtm` 时同时替换当前草稿的参考图、名称与参数。
3. 用属性编辑器设置角度、尺度、匹配分数、最多数量、目标最小间距、特征模式、遮挡比例、金字塔层数和亚像素定位。窗口“运行匹配”使用当前编辑值，显示分数、中心、角度、尺度、JSON 和临时四角叠加；运行前清空旧结果。换源图后需重新打开窗口核对 ROI，运行期间换图或关窗会丢弃显示结果。
4. 编辑器内用“导出模板”分享 `.cvtm`；节点配置窗口内用“应用到节点”提交草稿，再保存流程。“新建模板”开始新参考图和默认参数。节点窗口的“导出模板”只写文件，不代替“应用到节点”；取消或关闭窗口不会修改节点。

参数范围在托管层及 native 校验。参考图结构、无目标图和有干扰目标图都应进入业务样本复核；单张匹配成功不能替代误检漏检评估。

节点的 `ParameterJson` 与 `.cvtm` 共用 schema `colorvision.template-matching/v1`，包含名称、宽高、位深、通道、Base64 原始像素和完整参数。支持 8 位灰度/彩色、16 位灰度/RGB、32 位浮点灰度，最多 1600 万像素与 128 MiB 像素数据；浮点预览归一化不改变保存样本。导入拒绝未知版本、缺失必填对象、未知字段、尺寸/像素长度不一致及无效参数。导出先写同目录临时文件、刷新后替换；校验失败不覆盖原文件。导入后修改或删除原 `.cvtm` 不会影响节点；换机器时参考图和参数随流程携带；通过“加载图片”输入的待测文件需在目标机器可用。

## 本地流程节点

在流程编辑器“本地算法”选择“模板匹配”（`NodeType=LocalTemplateMatching`），连接 `IN → OUT`。在“匹配配置”旁点击“编辑…”调整节点内的参考图与参数，或导入其他人分享的 `.cvtm`；待测图片通过上游相机取图、校正或“加载图片”节点传入；“搜索区域”采用像素 `X,Y,Width,Height`，空值或全零代表全图，越界时报错。已有本地节点保留保存格式和连接；旧默认标题“本地模板匹配”加载时更新为“模板匹配”，自定义标题保留。

替换旧服务节点时需重新配置本地参考图与参数，并确认上游提供内存帧。旧服务的 AOI 数据库结果与本地 `LocalTemplateMatchingResult` 不同，下游依赖 AOI 结果 ID 的流程需要调整，不能直接交换节点类型或沿用服务参数模板。

每次运行从节点内的 JSON 建立本次快照，执行期间编辑配置不会改变该次匹配。输入为上游本地内存帧；仅传入旧图像记录 ID 而没有内存帧时明确报错，不查询数据库。RAW 使用交错 8/16 位灰度或 RGB，CIE 使用 32 位 Y 平面；方向变换未完成时拒绝执行。节点不改变源像素或上游帧的所有权，原帧继续交给下游。

成功后下游可读取 `LocalTemplateMatchingResult`、`LocalTemplateMatchingMatches`、`LocalTemplateMatchingBestMatch`，其中含整图中心、角度、尺度、分数、可见比例和四角。节点执行结果同时返回配置名称、内容指纹、本次参数、输入帧身份与耗时，供流程执行记录查看。此节点不创建数据库主表 ID、不发布旧服务结果事件，上游图像身份随输入继续传递；匹配结果不进入 AOI 历史表。编辑器的试匹配表格和临时叠加继续可用。流程宿主和其他上游节点自身的存储方式不由本节点改变。

无匹配、常量参考图等 native 拒绝结果会停止流程，不设置成功输出；无效配置/ROI 或调用异常同样停止。执行开始清除该节点的旧匹配数据。本地服务适配器只负责文件读帧和 native 调用，结果通过现有流程运行通道传递，独立于下述旧服务的 AOI DAO 与结果 handler。

<!-- algorithm-help:end -->

## 契约与源码

| 事项 | 当前实现 |
| --- | --- |
| 模板 / 参数 | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` / `MatchParam` |
| 字典 / 编码 | `TemplateDicId = 34` / `MatchTemplate` |
| Flow | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmTMNode.cs` |
| 结果 | `ViewResultAlgType.AOI`、`ViewHandleMatching`、`AlgResultAoiDao` |
| 明细表 | `t_scgd_algorithm_result_detail_aoi` |

上述模板文件位于 `Engine/ColorVision.Engine/Templates/Matching/`；模板编辑见 [模板入口](./template-menu-entries.md)。

## 持久参数与运行时输入

| `MatchParam` 字段 | 默认值 | 参数描述 |
| --- | --- | --- |
| `MinReducedArea` | `256` | 取样细致度，描述范围 64–2048 |
| `ToleranceAngle` | `0` | 误差角度，描述范围 0–180 |
| `Similarity` | `0.7` | 相似度阈值，描述范围 0–1 |
| `MaxOverlapRatio` | `0` | 交叠比例，描述范围 0–0.8 |
| `TargetNumber` | `70` | 目标数量 |

这些范围来自参数描述，不等于服务或编辑器已经强制校验。匹配用 `TemplateFile` 属于流程节点的运行时输入，不是 `MatchParam` 字段。

## Flow 请求

`AlgorithmTMNode` 使用 `TempName` 选择参数模板，`TemplateFile` 表示匹配文件，`ImgFileName` 表示输入；`operatorCode = MatchTemplate`。节点构造 `TMParam(TemplateFile)`，再通过 `BuildImageParam(...)` 补全图像参数。图像和参考模板文件须对服务可访问；服务生成 AOI 结果，不会进入 AA 找点链。

## AOI 结果

`ViewHandleMatching.Load` 在 `result.ViewResults == null` 时从 `AlgResultAoiDao.GetAllByPid(result.Id)` 加载明细。handler 取 AOI 四角，经 `GrahamScan.ComputeConvexHull` 计算凸包，用蓝色 `DVPolygon` 叠加，并生成分数、角度、中心与四角表格。

末尾两列分别为 `BottomLeftPointX` 和 `BottomLeftPointY`。源图恢复与 handler 选择遵循 [Engine 结果链](../../engine-components/result-handoff-chain.md)。

## 排查与变更约束

| 现象 | 证据入口 |
| --- | --- |
| 没有执行或路径无效 | 服务连接、请求事件、输入图/模板文件在服务端的可访问性 |
| 参数模板不生效 | 节点 `TempName` 与实际 `TemplateParam.ID/Name` |
| 结果表为空 | 主结果类型 AOI、`Id/pid` 对应关系、`ViewResults` 是否已缓存 |
| overlay 不对应原图 | 四角坐标系、输入图尺寸、当前展示源 |

改参数或结果字段时保留固定输入、模板文件和预期 AOI 四角样例；同时检查旧 Flow 路径及受影响的项目导出。参数范围、编译成功或类名存在均不能代替算法正确性验证。

## 验证入口与缺口

`LocalTemplateMatchingTests` 覆盖文件精度与版本/损坏校验、导出失败保留原文件、带步长的 ROI 复制、真实 native 合成图匹配、节点配置快照/序列化、删除导入文件后运行、无数据库执行、编辑草稿应用/取消、内存帧所有权和失败输出边界。native 的 `test_p2_algorithms.cpp` 另含旋转、缩放与遮挡用例。保留这些回归以保护可迁移配置及流程生命周期契约，测试文件存在不表示任意环境都已执行。

真实业务参考图、误检漏检范围与完整现场流程仍需单独验收；合成图不能替代这些证据。远端 Matching 请求、数据库结果以及流程宿主存储也不由本地用例验收。
