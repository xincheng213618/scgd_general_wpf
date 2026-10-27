## 发光区定位原理 {#algorithm-principles}

本地发光区定位 V2 用 `RobustV2` 从当前图像寻找有四边证据支持的发光区域，输出原图中的 **LT、RT、RB、LB** 四个几何角点。它不测量亮度均匀性或视场角，也不需要检测参数模板；成功四角可交给后续 FOV、布点和搜索区域使用。

<figure class="algorithm-figure" style="overflow-x:auto">
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 320" role="img" aria-labelledby="luminous-principle-title luminous-principle-desc" style="display:block;min-width:720px;width:100%;height:auto;color:#1f2937">
    <title id="luminous-principle-title">发光区从候选到四角的工作过程</title>
    <desc id="luminous-principle-desc">输入原图中限定搜索区域，多尺度阈值产生候选四边形，沿四边用卡尺采样并鲁棒拟合，最后检查逐边证据和总置信度，输出原图坐标 LT、RT、RB、LB。</desc>
    <defs><marker id="luminous-flow-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 Z" fill="#64748b" /></marker></defs>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="16">
      <text x="25" y="29">输入图像与搜索区域</text>
      <text x="355" y="29">由粗到精定位</text>
      <text x="655" y="29">原图中的四角</text>
    </g>
    <rect x="20" y="46" width="270" height="210" fill="#f8fafc" stroke="#94a3b8" />
    <rect x="53" y="69" width="205" height="160" fill="none" stroke="#64748b" stroke-dasharray="6 5" />
    <polygon points="80,85 234,99 223,197 89,211" fill="#e8f2ff" stroke="#2175d9" stroke-width="2" />
    <g stroke="#d38b25" stroke-width="2"><path d="M108 76 L105 102 M145 79 L142 105 M182 84 L179 110 M225 126 L247 128 M221 158 L242 160 M116 193 L119 219 M159 189 L162 215 M80 122 L102 120 M82 162 L104 160" /></g>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="14"><text x="30" y="282">灰虚框限定 ROI，输出仍为原图坐标</text></g>
    <rect x="335" y="75" width="215" height="60" rx="6" fill="#f1f5f9" stroke="#94a3b8" />
    <rect x="335" y="179" width="215" height="60" rx="6" fill="#f1f5f9" stroke="#94a3b8" />
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="16" text-anchor="middle"><text x="442" y="101">多尺度与多阈值</text><text x="442" y="123">寻找候选四边形</text><text x="442" y="205">沿四边卡尺取样</text><text x="442" y="227">鲁棒拟合边线与求交</text></g>
    <g fill="none" stroke="#64748b" stroke-width="2" marker-end="url(#luminous-flow-arrow)"><path d="M291 112 L328 105" /><path d="M442 139 L442 172" /><path d="M556 207 L609 166" /></g>
    <polygon points="650,95 842,80 854,221 624,212" fill="#e8f2ff" stroke="#2175d9" stroke-width="2" />
    <g fill="#2175d9"><circle cx="650" cy="95" r="5" /><circle cx="842" cy="80" r="5" /><circle cx="854" cy="221" r="5" /><circle cx="624" cy="212" r="5" /></g>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="16"><text x="630" y="80">LT</text><text x="838" y="64">RT</text><text x="849" y="245">RB</text><text x="602" y="236">LB</text><text x="625" y="282">逐边证据与总置信度均须通过</text></g>
  </svg>
  <figcaption>卡尺提供沿边的明暗边缘证据，四条拟合边线的交点形成最终四角。</figcaption>
</figure>

### 工作步骤与坐标 {#luminous-steps}

1. 借用上游图像，确认方向变换已经完成，再解析整图或指定搜索区域。
2. 归一化图像，在多尺度、多阈值候选中寻找四边形。
3. 沿粗边用卡尺采样和鲁棒直线拟合细化边界。
4. 检查角点几何、逐边证据和总置信度；通过后还原为原图坐标。
5. 按需回写已有 POI 模板，随后提交本次算法结果，供后续节点引用。

ROI 只限定搜索范围。若 ROI 内的局部角点为 $P_{ROI}$，ROI 左上角为 $(x_0,y_0)$，输出为：

$$
P_{image}=P_{ROI}+(x_0,y_0)
$$

例如 ROI 左上角为 `(100,50)`，局部 LT 为 `(20.5,10.25)`，最终 LT 为原图的 `(120.5,60.25)`，单位为 **px**。下游使用同一幅已完成方向变换的图像，不能再把这些点当作 ROI 局部坐标或变换前坐标。

## 配置本地发光区定位(V2)

在流程中加入 **本地发光区定位(V2)**，默认使用上游图像：接入相机取图或校正节点，本地文件通过 **加载图片 → 发光区定位** 输入。图像方向变换必须已完成；节点借用当前图像计算，不修改源像素。没有可用上游图像时明确报错，并提示连接取图、校正或加载图片节点。

| 界面选项 | 属性 / 默认值 | 使用方式 |
| --- | --- | --- |
| 搜索区域 | `SearchRegion` / 整图 | `0,0,0,0` 表示整图；指定 ROI 时宽高必须大于零且完全位于图像内，输出还原为整图坐标 |
| 最小置信度 | `MinimumConfidence` / `0.25` | 必须是 `0` 到 `1` 的有限数值；低于门限拒绝结果，不会静默改用旧算法 |
| POI保存模板 | `SavePOITempName` / 空 | 可选的数据库回写目标，见下文；不是检测输入参数模板 |

运行包含该节点的流程后，检查 `Success`、置信度、警告与四角顺序，并在历史结果中核对本次主结果。成功四角按 **LT、RT、RB、LB** 保存；生成四个坐标本身不能作为成功判据。

`Confidence` 是 0～1 的定位质量评分，不是检测正确率概率。最低门限包含等于，例如门限 `0.25` 时，`0.23` 被拒绝；通过总分门限仍必须满足角点及逐边检查。`SideQuality` 保留逐边覆盖、内点、对比度、残差和缺口等诊断，供区分遮挡、弱边缘和拟合不稳定。

### 图像来源与格式

节点按以下优先级使用上游图像：

1. 当前上游本地内存帧。
2. `IN` 输入图像结果；没有有效输入引用时读取流程数据中的 `MasterId`、`MasterResultType`。结果类型需为相机图像或算法校正，读取对应 `FileUrl`、`RawFile` 中存在的文件。

已选来源存在但无效时会报错，不会一路忽略错误尝试后续来源。例如上游图像结果指向的文件不存在时直接报错；声明 CIE 主缓冲却缺少 CIE 数据时，也不会改用 RAW。成功从文件加载的帧会交给后续节点，使角点与图像保持同源。

本地 RAW 输入支持 8/16 位、1/3 通道交错缓冲；CIE 支持 32 位浮点单平面 Y 或三平面 XYZ，三平面只借用第二个 Y 平面。文件加载器先识别 ColorVision 文件头，否则交给 WPF 位图解码器；支持的位图像素格式包括 Gray8、Gray16、Rgb48、24/32 位彩色，并非只接受 `.cvraw` / `.cvcie` 扩展名。

### 回写 POI 保存模板

填写 **POI保存模板** 会在定位成功后更新已有 POI 模板的数据库明细；留空则不回写。模板按去除首尾空白的名称、租户 `0`、未删除条件查询，同名时取最小 ID；`IsEnable` 不参与筛选。

- 一个 `Rect` 或 `LTRect` 明细：写入四角的外接矩形；前者记录中心，后者记录左上角。宽高采用 `floor(max) - floor(min) + 1`。
- 四个角点明细：按明细 ID 顺序写入 LT、RT、RB、LB，坐标转为整数，宽高归零。按服务的 `POIPointTypes` 判断类型；首行允许 `PolygonFour` 或兼容的 `LTRect` 数值表示，写回时统一为 `PolygonFour`。
- 其它形状、明细数量或不存在的模板会报错，不会自动创建新模板。

下游本地十字定位和本地点阵畸变 V2 的“搜索区域关注点”可选择同一模板，按最新保存明细生成搜索矩形，优先于节点固定区域。四角点取外接矩形，不透视裁正；模板读写遵循服务点类型数值。节点不缓存坐标，但模板不携带帧身份，也不能证明本轮前序检测成功；流程必须保证前序成功后再执行，并行流程使用不同模板以避免互相覆盖。

POI 回写有自己的事务，且在算法结果事务之前提交。后续结果保存失败不会撤销已提交的 POI 修改；这两步不是一个整体事务。

### 结果提交与失败状态

成功路径为：检测与四角校验 → 可选 POI 回写 → 主结果及四条明细提交 → 设置流程主结果引用 → 发布结果消息。主结果和明细使用同一事务，数据库表需支持事务；若保存或回滚失败，异常会向上抛出。

| 失败位置 | 可观察结果 |
| --- | --- |
| 检测返回拒绝或四角校验失败 | 尝试保存 `ResultCode = -1` 的失败主结果及诊断参数，不保存角点明细；发布该失败记录后仍按失败结束，不设置本次可用主结果引用 |
| 图像输入、ROI、方向变换或前置配置检查失败 | 尚未进入上述失败记录保存分支，不能保证历史结果中已有失败主记录 |
| POI 回写或结果保存失败 | 不继续发布成功结果；已先提交的 POI 回写不随结果保存失败回滚 |
| 成功结果提交后消息发布失败 | 已提交的结果和流程主结果引用保留，节点仍抛出异常；应核对结果 ID，避免重复执行造成重复数据 |

保存成功与保存失败主结果都需要按流程流水号找到批次。找不到批次、数据库不可用或消息发布异常时，不能假定失败记录已经出现在“算法结果管理”。

## RobustV2 的结果判据

默认允许尝试边界目标并附加警告；角点几何、逐边证据和总置信度仍需通过检查。遮挡、裁边、多候选或无有效信号均可能导致拒绝，不应仅凭叠图判断业务可接受。

| 调用层 | 成功与结果边界 |
| --- | --- |
| C++ `cvnative::luminous::FindLuminousAreaV2` | 返回 `FindLuminousAreaV2Result`，检查 `success` 与 `hasCorners`；`corners` 是固定四元素数组，默认零坐标也占四项。拒绝仍可能保留诊断角点、置信度、原因和警告 |
| ABI `M_FindLuminousAreaV2` | 正返回值表示产生 JSON，不表示 `Success=true`；仅在 `hasCorners` 为真时填写 `Corners`。结果指针按 `FreeResult` 契约释放；参数或调用错误与算法拒绝分别处理 |
| 托管 `LuminousAreaNative.DetectV2` | 解析 JSON、校验成功结果几何、还原 ROI 坐标并执行置信度门限。`HasValidCorners` 要求 `Success` 且角点几何有效，不因诊断角点存在而把失败改为成功 |

ImageEditor、POI 的配置对象默认使用 `RobustV2`，旧配置缺少 `Algorithm` 字段时也保持此默认值；显式选择经典兼容模式才显示 `Threshold`、`UseRotatedRect`。本地 V2 Flow 节点固定使用 RobustV2。

ImageEditor、POI 还可显式选择 `FovLuminanceBoundary`（界面名 **实验：中心亮度比例边界（非几何 FOV）**）。它先调用 `RobustV2`，再以中心亮度乘 `LuminanceBoundaryRatio`（默认 `0.5`）寻找亮度交点并拟合四边。内部暗角和渐变可能使角点明显内缩，该实验方法不代表标准有效视场。普通发光区和独立 FOV 均默认使用 `RobustV2` 几何边缘；FOV 有上游四角时直接复用，不调用此实验算法。