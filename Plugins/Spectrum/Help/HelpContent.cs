namespace Spectrum.Help
{
    /// <summary>
    /// Category of help content
    /// </summary>
    public enum HelpCategory
    {
        Terminology,
        Usage
    }

    /// <summary>
    /// A single help entry containing a term/topic and its explanation.
    /// </summary>
    public class HelpEntry
    {
        /// <summary>
        /// Category: terminology explanation or usage guide
        /// </summary>
        public HelpCategory Category { get; set; }

        /// <summary>
        /// Display title (e.g. "显色性指数 Ra", "自动校零")
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Short summary shown in the list view
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Full explanation text with formulas and details
        /// </summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>
        /// Searchable keywords (comma-separated)
        /// </summary>
        public string Keywords { get; set; } = string.Empty;

        public string CategoryDisplay => Category == HelpCategory.Terminology ? "📖 专业术语" : "🔧 使用指南";
    }

    /// <summary>
    /// Provides all built-in help entries for the Spectrum application.
    /// </summary>
    public static class HelpData
    {
        public static List<HelpEntry> GetAllEntries()
        {
            var entries = new List<HelpEntry>();

            // ========================================
            // 专业术语 (Professional Terminology)
            // ========================================

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "显色性指数 Ra (Color Rendering Index)",
                Summary = "衡量光源对物体颜色还原能力的指标",
                Keywords = "Ra,CRI,显色性,色彩还原,显色指数,R1-R15",
                Detail = @"显色性指数 Ra（Color Rendering Index, CRI）

【定义】
Ra 是衡量光源对物体颜色还原能力的国际标准指标，取值范围 0–100。Ra = 100 表示被测光源与参考光源（自然光或黑体辐射体）的显色效果完全一致。

【计算方法】
1. 选取 CIE 规定的 8 种标准色样（R1–R8）
2. 分别在参考光源和被测光源下计算每个色样的色度坐标
3. 计算每个色样的特殊显色指数 Ri = 100 − 4.6 × ΔEi
   其中 ΔEi 为色差（在 CIE 1964 W*U*V* 均匀色空间中）
4. Ra = (R1 + R2 + ... + R8) / 8

【参考标准】
• CIE 13.3-1995
• Ra ≥ 90：高显色性，适合博物馆、医疗
• Ra ≥ 80：一般照明
• Ra < 60：不适合对颜色敏感的场景

【扩展】
R9–R15 为补充色样的特殊显色指数：
• R9（饱和红色）：对 LED 特别重要
• R13（肤色）：对人像照明重要"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "外量子效率 EQE (External Quantum Efficiency)",
                Summary = "LED 将电子转换为光子的效率",
                Keywords = "EQE,量子效率,external quantum efficiency,光子,电子",
                Detail = @"外量子效率 EQE（External Quantum Efficiency）

【定义】
EQE 是衡量 LED / 光源将注入电子转换为发射光子能力的关键指标，定义为：

  EQE = 发射到外部的光子数 / 注入的电子数

【计算公式】
  EQE = N_photon / N_electron

其中：
  N_electron = I / q    （I 为电流，q = 1.602×10⁻¹⁹ C 为元电荷）
  N_photon = Σ [ S(λ) × fPlambda × Δλ × λ × 10⁻⁹ ] / (h × c)
    • S(λ) = 相对光谱分布
    • fPlambda = 绝对光谱系数（W/nm）
    • h = 6.626×10⁻³⁴ J·s（普朗克常数）
    • c = 2.998×10⁸ m/s（光速）
    • λ = 波长（nm）
    • Δλ = 波长步长（通常 0.1nm 或 1nm）

【典型值】
• 高性能蓝光 LED：60–80%
• 红光 LED：30–50%
• 白光 LED（荧光粉）：20–40%

【使用场景】
在本软件的「光通量模式」下，需输入电压(V)和电流(mA)后自动计算 EQE。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "主波长 Ld (Dominant Wavelength)",
                Summary = "表征光源颜色感知的单色波长",
                Keywords = "主波长,Ld,dominant wavelength,色度坐标",
                Detail = @"主波长 Ld（Dominant Wavelength）

【定义】
主波长是 CIE 色度图上，从光源白点（通常为 D65：x=0.3127, y=0.3290）通过被测光源色度点的延长线与光谱轨迹交点所对应的波长。它表征人眼对该光源颜色的感知。

【几何意义】
在 CIE 1931 色度图中：
1. 从等能白点 E（或 D65 白点）向被测样品色度点 (x, y) 画一条射线
2. 该射线与光谱轨迹（马蹄形曲线）的交点对应的波长即为主波长
3. 若延长线交于紫色线（非光谱色），则取反方向交点，加负号或标注 'c'（补色主波长）

【单位】
纳米（nm），可见光范围约 380–780 nm

【应用】
• LED 分 BIN（色度分选）
• 判断光源颜色属性（如 590nm 对应琥珀色）"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "峰值波长 Lp (Peak Wavelength)",
                Summary = "光谱功率分布最大值对应的波长",
                Keywords = "峰值波长,Lp,peak wavelength,光谱峰值",
                Detail = @"峰值波长 Lp（Peak Wavelength）

【定义】
峰值波长是光谱功率分布（SPD）中辐射功率最大值所对应的波长。

【与主波长的区别】
• 峰值波长 Lp：纯粹的物理量，光谱最大值位置
• 主波长 Ld：基于人眼色觉感知的心理物理量

对于窄带 LED，Lp ≈ Ld；
对于宽带或多峰光源，两者可能相差较大。

【单位】
纳米（nm）"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "半波宽 FWHM (Full Width at Half Maximum)",
                Summary = "光谱峰值一半处的波长宽度",
                Keywords = "半波宽,FWHM,半高全宽,带宽,spectral width",
                Detail = @"半波宽 FWHM（Full Width at Half Maximum）

【定义】
半波宽（半高全宽）是光谱功率分布曲线中，峰值功率一半处的两个波长之差。

【计算方法】
1. 找到光谱最大值 P_max
2. 在峰值两侧找到 P = P_max / 2 的两个波长 λ₁ 和 λ₂
3. FWHM = λ₂ − λ₁

【意义】
• FWHM 越小，光谱越窄，颜色越纯
• 典型值：红光 LED 约 15–25nm，蓝光 LED 约 20–30nm
• 荧光粉激发的白光 LED：FWHM 可达 100nm 以上

【单位】
纳米（nm）"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "相关色温 CCT (Correlated Color Temperature)",
                Summary = "描述光源色觉冷暖感的温度值",
                Keywords = "色温,CCT,correlated color temperature,开尔文,暖白,冷白",
                Detail = @"相关色温 CCT（Correlated Color Temperature）

【定义】
相关色温是用黑体辐射体的温度来近似描述非黑体光源颜色外观的物理量。
在 CIE 1960 UCS 色度图上，被测光源色度点到普朗克轨迹最近点对应的黑体温度。

【计算方法】
1. 将被测光源色度坐标 (x, y) 转换到 CIE 1960 UCS (u, v) 坐标
2. 计算 (u, v) 到普朗克轨迹上各温度点的距离
3. 最近点对应的温度即为 CCT

常用近似公式（McCamy 公式）：
  n = (x − 0.3320) / (0.1858 − y)
  CCT = 449n³ + 3525n² + 6823.3n + 5520.33

【单位】
开尔文（K）

【参考值】
• 1800K：烛光
• 2700K：暖白光
• 4000K：中性白光
• 5000K：日光
• 6500K：正午日光 / D65 标准光源
• >8000K：蓝天光"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "色度坐标 CIE 1931 (x, y)",
                Summary = "CIE 1931 色度空间中的颜色坐标",
                Keywords = "色度坐标,CIE1931,xy,色品坐标,chromaticity,三刺激值,XYZ",
                Detail = @"色度坐标 CIE 1931 (x, y)

【定义】
CIE 1931 色度坐标是从三刺激值 (X, Y, Z) 归一化得到的二维坐标，用于描述颜色的色品（色调+饱和度）。

【计算公式】
  x = X / (X + Y + Z)
  y = Y / (X + Y + Z)
  z = Z / (X + Y + Z) = 1 − x − y

其中三刺激值：
  X = K × ∫ S(λ) × x̄(λ) dλ
  Y = K × ∫ S(λ) × ȳ(λ) dλ
  Z = K × ∫ S(λ) × z̄(λ) dλ

• S(λ) = 光源光谱功率分布
• x̄(λ), ȳ(λ), z̄(λ) = CIE 1931 标准观察者颜色匹配函数
• K = 归一化常数

【色度图】
所有可见颜色位于马蹄形区域内。边界（光谱轨迹）对应单色光。

【参考标准】
• CIE 15:2004
• 2° 标准观察者（CIE 1931）
• 10° 补充标准观察者（CIE 2015）"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "色度坐标 CIE 1976 (u', v')",
                Summary = "CIE 1976 均匀色度空间中的颜色坐标",
                Keywords = "色度坐标,CIE1976,u'v',UCS,均匀色度",
                Detail = @"色度坐标 CIE 1976 (u', v')

【定义】
CIE 1976 UCS（Uniform Chromaticity Scale）色度坐标是对 CIE 1931 xy 坐标的均匀化变换，使得色度图上的距离更好地反映人眼的颜色差异感知。

【转换公式】
  u' = 4X / (X + 15Y + 3Z) = 4x / (−2x + 12y + 3)
  v' = 9Y / (X + 15Y + 3Z) = 9y / (−2x + 12y + 3)

【优势】
在 CIE 1931 xy 色度图中，绿色区域过大，相同的色差在不同区域对应不同的 xy 距离。CIE 1976 u'v' 图更均匀，色差的几何距离更一致。

【应用】
• LED 色度一致性评估（ANSI binning）
• 色差计算 Δu'v'
• 相关色温计算的中间步骤"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "色纯度 (Color Purity)",
                Summary = "表征颜色接近光谱色的程度",
                Keywords = "色纯度,color purity,纯度,fPur",
                Detail = @"色纯度（Color Purity）

【定义】
色纯度表征一个颜色在 CIE 色度图上距白点多远（相对于光谱轨迹）。色纯度越高，颜色越饱和、越接近单色光。

【计算方法】
在 CIE 1931 色度图中：
  p = d_sample / d_spectrum

其中：
• d_sample = 白点到样品点的距离
• d_spectrum = 白点到光谱轨迹（沿同一方向）的距离

【取值范围】
• 0%：白色（与白点重合）
• 100%：纯光谱色（位于光谱轨迹上）

【与兴奋纯度的区别】
色纯度使用亮度加权的距离，兴奋纯度使用色度坐标的欧几里得距离。两者对大多数窄带 LED 非常接近。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "兴奋纯度 pe (Excitation Purity)",
                Summary = "CIE 色度图上基于几何距离的颜色纯度",
                Keywords = "兴奋纯度,excitation purity,pe,纯度",
                Detail = @"兴奋纯度 pe（Excitation Purity）

【定义】
兴奋纯度是在 CIE 1931 色度图上，从参考白点到被测样品色度点的距离与从白点到主波长对应的光谱轨迹点距离的比值。

【计算公式】
  pe = √[(x − xn)² + (y − yn)²] / √[(xd − xn)² + (yd − yn)²]

其中：
• (x, y) = 被测样品色度坐标
• (xn, yn) = D65 白点（0.3127, 0.3290）
• (xd, yd) = 主波长在光谱轨迹上的色度坐标

【本软件实现】
使用 ColorimetryHelper.CalculateExcitationPurity() 方法，基于主波长查找光谱轨迹坐标，然后按上述公式计算。

【取值范围】
0（白色）到 1（纯光谱色）。显示时乘以 100 转为百分比。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "光通量 Φv (Luminous Flux)",
                Summary = "光源在所有方向上发出的总光能量（视觉加权）",
                Keywords = "光通量,luminous flux,流明,lm,Φv,fPh",
                Detail = @"光通量 Φv（Luminous Flux）

【定义】
光通量是光源在所有方向上发射的，经人眼视觉光谱响应函数 V(λ) 加权后的总辐射功率。

【计算公式】
  Φv = Km × ∫ S(λ) × V(λ) dλ

其中：
• Km = 683 lm/W（最大光视效能，对应 555nm）
• S(λ) = 绝对光谱功率分布（W/nm）
• V(λ) = CIE 光视效率函数

【单位】
流明（lm）

【在本软件中】
光通量模式下，软件直接使用 C++ 计算的 fPh 值作为光通量。该值由硬件底层对绝对光谱进行 V(λ) 加权积分得到。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "辐射通量 Φe (Radiant Flux)",
                Summary = "光源发出的总辐射功率",
                Keywords = "辐射通量,radiant flux,辐射功率,瓦特,W,Φe",
                Detail = @"辐射通量 Φe（Radiant Flux）

【定义】
辐射通量是光源在所有方向上发射的总辐射功率，不考虑人眼视觉响应。

【计算公式】
  Φe = Σ [ S(λ) × Δλ ]
     = Σ [ fPlambda × fPL[i] × Δλ ]

其中：
• fPlambda = 绝对光谱系数
• fPL[i] = 相对光谱值
• Δλ = 波长步长（1nm 或 0.1nm）

【单位】
瓦特（W）

【本软件实现】
在光通量模式下，程序对每个波长点的绝对光谱值进行数值积分求和。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "光效 (Luminous Efficacy)",
                Summary = "光通量与电功率之比",
                Keywords = "光效,luminous efficacy,lm/W,发光效率",
                Detail = @"光效（Luminous Efficacy）

【定义】
光效是光源的光通量与消耗的电功率之比，衡量电能到可见光的转换效率。

【计算公式】
  η = Φv / P_elec = 光通量(lm) / 电功率(W)
  其中 P_elec = V × I（电压 × 电流）

【本软件实现】
  光效 = fPh / (V × I / 1000)
  • fPh = 光通量（lm）
  • V = 电压（V）
  • I = 电流（mA），除以 1000 转为 A

【单位】
流明每瓦（lm/W）

【典型值】
• 白炽灯：10–17 lm/W
• 荧光灯：50–100 lm/W
• 白光 LED：100–200 lm/W
• 理论最大值（555nm 单色光）：683 lm/W"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "亮度 Lv (Luminance)",
                Summary = "单位面积、单位立体角的光通量",
                Keywords = "亮度,luminance,Lv,cd/m2,坎德拉",
                Detail = @"亮度 Lv（Luminance）

【定义】
亮度是单位投影面积在指定方向上单位立体角内发出的光通量，描述发光面的明亮程度。

【单位】
坎德拉每平方米（cd/m²）= 尼特（nit）

【与光通量的区别】
• 光通量 Φv：光源在所有方向上发出的总光量（lm）
• 发光强度 Iv：在某方向上单位立体角内的光通量（cd）
• 亮度 Lv：发光面在某方向上单位投影面积的发光强度（cd/m²）

【在本软件中】
亮度值 (fPh) 在光谱仪测量后由 C++ 底层计算得出。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "色差 dC",
                Summary = "被测光源色度点偏离普朗克轨迹的距离",
                Keywords = "色差,dC,Duv,偏差",
                Detail = @"色差 dC (Duv)

【定义】
色差 dC（也称 Duv）是被测光源在 CIE 1960 UCS 色度图上偏离普朗克（黑体）轨迹的距离。

【意义】
• dC > 0：偏向绿色（偏暖）
• dC < 0：偏向洋红色（偏冷）
• |dC| 越小，光源越接近理想黑体辐射的颜色

【应用】
• LED 色温 BIN 的质量控制
• 通常要求 |Duv| < 0.006 才算在对应色温的标准范围内"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "三刺激值 CIE XYZ",
                Summary = "CIE 标准色度系统的基本色度量",
                Keywords = "三刺激值,XYZ,CIE,tristimulus,标准观察者",
                Detail = @"三刺激值 CIE XYZ (Tristimulus Values)

【定义】
三刺激值 X、Y、Z 是描述人眼颜色感知的三个基本量，对应 CIE 定义的三个虚拟的原色刺激量。

【计算公式】
  X = K × ∫ S(λ) × x̄(λ) dλ
  Y = K × ∫ S(λ) × ȳ(λ) dλ
  Z = K × ∫ S(λ) × z̄(λ) dλ

• x̄(λ), ȳ(λ), z̄(λ) = CIE 颜色匹配函数
• Y 同时与亮度成正比

【CIE 2015 10° 标准观察者】
本软件同时计算 CIE 2015 标准的三刺激值和色度坐标，用于更精确的大视场颜色评估。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "蓝光比 (Blue Light Ratio)",
                Summary = "蓝光波段能量占总可见光能量的比例",
                Keywords = "蓝光,blue light,蓝光危害,蓝光比",
                Detail = @"蓝光比（Blue Light Ratio）

【定义】
蓝光比是 415–535nm 波段的光谱积分值占 400–700nm 全可见光波段积分值的百分比。

【计算方法】
  蓝光比 = Σ S(415–535nm) / Σ S(400–700nm) × 100%

【意义】
蓝光（380–500nm）可能对视网膜造成光化学损伤（IEC 62471 蓝光危害标准）。
蓝光比越高，长时间暴露的风险越大。

【应用】
• 显示器、LED 照明的蓝光评估
• 防蓝光产品的效果验证"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "绝对光谱系数 fPlambda",
                Summary = "将相对光谱转换为绝对光谱功率的系数",
                Keywords = "fPlambda,绝对光谱,标定系数,绝对校准",
                Detail = @"绝对光谱系数 fPlambda

【定义】
绝对光谱系数是通过标准灯标定后得到的系数。将相对光谱值 fPL[i] 乘以 fPlambda 即可得到每个波长处的绝对光谱功率（W/nm）。

【公式】
  P(λ) = fPL[i] × fPlambda    (W/nm)

【来源】
通过使用标准灯（已知绝对光谱功率分布）对光谱仪进行辐射标定得到。标定文件（幅值文件 Magiude.dat）存储了该系数。

【应用】
• 绝对光谱测量
• 辐射通量计算
• 光通量计算的基础"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Terminology,
                Title = "IP (积分峰值比)",
                Summary = "光谱峰值AD值与满量程的比例",
                Keywords = "IP,积分峰值,峰值AD,信号强度,fIp",
                Detail = @"IP（积分峰值比）

【定义】
IP 是光谱仪采集到的峰值 AD 值（fIp）与满量程（65535）的比值，以百分比表示。

【计算公式】
  IP = fIp / 65535 × 100%

【意义】
• IP 反映了光谱信号的强度
• IP 过低（<10%）：信号太弱，信噪比差
• IP 过高（>90%）：可能饱和，需要减小积分时间
• 推荐范围：30%–80%"
            });

            // ========================================
            // 使用指南 (Usage Guides)
            // ========================================

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "同步频率测量",
                Summary = "消除因 PWM 驱动导致的光谱闪烁误差",
                Keywords = "同步频率,sync frequency,PWM,闪烁,syncfreq",
                Detail = @"同步频率测量

【场景】
当被测光源使用 PWM（脉冲宽度调制）驱动时，光源会产生周期性的亮灭，导致普通积分测量结果不稳定。

【原理】
同步频率测量模式下，光谱仪的积分时间会自动对齐到 PWM 信号的整数个周期，从而消除闪烁对测量结果的影响。

【使用方法】
1. 在控制面板中勾选「同步频率」选项
2. 设置频率值（Hz），应与被测光源的 PWM 驱动频率一致
3. 设置频率因子（通常为 1）
4. 点击测量，软件会自动调整积分时间为频率周期的整数倍

【参数说明】
• 同步频率（Hz）：被测光源的 PWM 频率
• 频率因子：积分时间 = 因子 × (1/频率)，通常设为 1
• 积分时间：开启同步频率后，实际积分时间由系统自动调节

【注意事项】
• PWM 频率需要准确设置，否则仍可能残留闪烁误差
• 此模式下自动积分时间功能可能会修改积分时间
• 适用于 LED 屏幕、LED 驱动电路等 PWM 调光的光源"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "自动校零（暗电流校正）",
                Summary = "校正光谱仪的暗电流和背景噪声",
                Keywords = "校零,dark,暗电流,暗噪声,校正,dark storage,shutter",
                Detail = @"自动校零（暗电流校正）

【原理】
光谱仪的探测器即使在无光照条件下也会产生暗电流信号。校零（Dark Calibration）是在遮光条件下采集一组暗电流数据，后续每次测量时自动减去暗电流，消除背景噪声。

【操作步骤】
1. 确保积分时间和平均次数已设定好
2. 方法一（手动）：遮住光谱仪入光口 → 点击「校零」按钮
3. 方法二（自动）：勾选「自动校零」→ 需要连接快门控制器(Shutter)，测量前自动开关快门进行校零

【最佳实践】
• 更换积分时间后应重新校零
• 光谱仪刚通电时建议先预热 10–15 分钟再校零
• 在温度变化较大的环境中，建议每 30 分钟重新校零

【自动校零功能】
勾选「自动校零」后，每次测量前会自动：
1. 关闭快门（shutter）
2. 采集暗电流数据
3. 打开快门
4. 进行正式测量
需要硬件配备快门控制器。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "自适应校零",
                Summary = "在不同积分时间下自动生成匹配的暗电流数据",
                Keywords = "自适应校零,adaptive dark,自适应暗电流,adaptive auto dark",
                Detail = @"自适应校零（Adaptive Auto Dark）

【原理】
传统校零只对应一个固定积分时间。当使用自动积分时间功能时，积分时间会动态变化，单次校零数据不再适用。自适应校零通过在多个预设的积分时间点采集暗电流数据，构建一个暗电流 - 积分时间的映射表，从而在任意积分时间下都能获得准确的暗电流校正。

【使用方法】
1. 点击齿轮图标 → 打开参数设置
2. 设置自适应校零参数：
   • 起始积分时间 (fTimeStart)：最小积分时间
   • 步进时间 (nStepTime)：每步增加的时间
   • 步数 (nStepCount)：采集多少组暗电流数据
3. 点击「执行自适应校零」
4. 勾选「自适应校零」选项，之后的测量会自动使用

【参数建议】
• 起始时间：根据光谱仪型号设置，通常 10–50ms
• 步进：通常 50–100ms
• 步数：5–10 步

【适用场景】
• 需要同时使用「自动积分时间」功能
• 被测样品亮度变化范围大
• 连续测量中需要自动调节积分时间"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "光通量模式 (EQE 模式)",
                Summary = "测量 LED 的光电参数：EQE、光通量、辐射通量、光效",
                Keywords = "光通量模式,EQE模式,LED测量,源表,SMU,电流,电压",
                Detail = @"光通量模式（EQE 模式）

【简介】
光通量模式用于完整表征 LED 的光电性能，在测量光谱的同时记录电参数，计算 EQE、光通量、辐射通量和光效。

【开启方式】
在菜单栏右侧勾选「光通量模式」复选框。

【输入参数】
• 电压 V（伏特）：LED 正向电压
• 电流 I（毫安）：LED 正向电流
  可手动输入，也可通过源表（SMU）自动读取。

【计算内容】
1. **EQE（外量子效率）**：光子数 / 电子数
2. **光通量（lm）**：由 C++ 底层 V(λ) 加权积分得到
3. **辐射通量（W）**：绝对光谱的数值积分
   Φe = Σ [fPlambda × fPL[i]] × Δλ
4. **光效（lm/W）**：光通量 / 电功率
   η = Φv / (V × I / 1000)

【源表（SMU）连接】
1. 在「源表」区域设置设备名和连接方式
2. 点击「连接源表」
3. 设置源表参数（电压/电流输出模式）
4. 测量时自动读取 V 和 I 值

【重新计算】
可选择已有数据条目，修改 V/I 后点击「重新计算」，软件会重新计算 EQE 和光效。

【导出】
EQE 模式下导出的 CSV 包含 EQE(%)、光通量、辐射通量、光效、电压、电流等字段。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "自动积分时间",
                Summary = "自动调整积分时间以获得最佳信噪比",
                Keywords = "自动积分,auto integration,积分时间,信噪比,auto time",
                Detail = @"自动积分时间

【原理】
光谱仪需要合适的积分时间才能获得高信噪比的光谱数据。积分时间过短，信号弱；过长，可能饱和。自动积分时间功能让光谱仪自动寻找最佳积分时间。

【使用方法】
1. 方式一：点击「自动积分」按钮，单次自动调节
2. 方式二：勾选「自动积分」选项，每次测量前自动调节

【参数说明】
• 积分时间上限 (IntLimitTime)：最长允许的积分时间
• 自动积分系数 (AutoIntTimeB)：调节灵敏度
• 目标最大值 (Max / MaxPercent)：期望的信号峰值百分比

【工作流程】
1. 光谱仪先用初始积分时间采集一次
2. 根据信号峰值与目标值的比例，计算新的积分时间
3. 迭代调整，直到信号处于最佳范围

【注意事项】
• 搭配「自适应校零」可避免积分时间变化后暗电流不匹配
• 积分时间上限不宜设置过大，否则单次测量耗时长"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "连续测量",
                Summary = "自动执行多次测量，用于稳定性和一致性评估",
                Keywords = "连续测量,continuous,loop,batch,批量",
                Detail = @"连续测量

【功能】
自动连续执行指定次数的光谱测量，用于评估光源的稳定性、老化特性或进行批量数据采集。

【使用方法】
1. 设置「测量次数」（0 表示无限循环）
2. 设置「测量间隔」（毫秒）
3. 点击「连续测量」按钮开始
4. 进度条显示当前进度和预估剩余时间
5. 点击「停止」按钮终止

【显示信息】
• 进度条：当前进度百分比
• 已用时间：从开始到现在经过的时间
• 剩余时间：根据平均速度估算的剩余时间

【注意事项】
• 连续测量期间，校零、单次测量等按钮会被禁用
• 如开启了自动校零，每次测量前都会执行校零
• 测量数据会实时保存到数据库
• 建议在光源预热稳定后再开始连续测量"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "标定文件管理",
                Summary = "波长标定文件和幅值标定文件的用途和管理",
                Keywords = "标定,calibration,波长文件,幅值文件,WavaLength,Magiude",
                Detail = @"标定文件管理

【波长标定文件 (WavaLength.dat)】
• 作用：将光谱仪的像素位置映射到精确的波长值
• 来源：出厂时使用汞灯等标准光源标定
• 加载：连接光谱仪时自动加载

【幅值标定文件 (Magiude.dat)】
• 作用：提供绝对光谱校准系数（fPlambda）
• 来源：使用标准灯（已知绝对光谱分布）标定
• 加载：连接光谱仪时自动加载

【标定组管理】
• 不同光谱仪序列号(SN)可对应不同的标定文件组
• 通过「标定组管理」功能可以为每个 SN 配置专属的波长文件和幅值文件
• 支持添加、编辑、删除标定组

【生成幅值标定文件】
工具菜单 → 「生成幅值标定文件」可根据标准灯数据生成新的幅值文件。"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "SP100 参数设置",
                Summary = "高级光谱处理参数配置",
                Keywords = "SP100,emission,参数设置,nStartPos,nEndPos,dMeanThreshold",
                Detail = @"SP100 参数设置

【功能】
SP100 是光谱仪内部的高级光谱处理参数，用于控制光谱数据的预处理。

【参数说明】
• IsEnabled：是否启用 SP100 处理
• nStartPos：光谱处理起始位置
• nEndPos：光谱处理结束位置  
• dMeanThreshold：均值阈值

【设置方式】
点击右上角的齿轮图标打开 SP100 参数设置窗口。修改参数后，如已连接光谱仪，参数会立即应用。

【注意事项】
• 这些参数通常由设备出厂配置，非专业人员不建议修改
• 参数变更后会自动保存到配置文件"
            });

            entries.Add(new HelpEntry
            {
                Category = HelpCategory.Usage,
                Title = "数据导出",
                Summary = "将测量数据导出为 CSV 文件",
                Keywords = "导出,export,CSV,Excel,数据",
                Detail = @"数据导出

【功能】
将选中的测量数据导出为 CSV 格式文件，可在 Excel 或其他数据分析工具中打开。

【操作方法】
1. 在数据列表中选择要导出的数据（可多选）
2. 右键 → 「导出」或使用快捷键
3. 选择保存路径和文件名

【导出内容 - 普通模式】
序号、IP、亮度、蓝光比、CIE XYZ、色度坐标、色温、主波长、色纯度、峰值波长、Ra、FWHM、兴奋纯度、主波长颜色、CIE 2015 参数、相对光谱(380-780nm)、绝对光谱(380-780nm)

【导出内容 - 光通量模式】
序号、IP、EQE(%)、光通量、辐射通量、光效、色度坐标、色温、峰值波长、兴奋纯度、主波长颜色、电压、电流、CIE 2015 参数、相对光谱、绝对光谱

【快捷操作】
• Ctrl+C：复制选中行数据（带表头）
• Ctrl+A：全选
• Delete：删除选中数据"
            });

            return entries;
        }
    }
}
