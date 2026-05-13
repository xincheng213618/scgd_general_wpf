using System.Collections.Generic;
using System.Text;

namespace Conoscope.Help
{
    public enum ConoscopeHelpCategory
    {
        QuickStart,
        Workflow,
        Principle,
        Terminology
    }

    public enum ConoscopeHelpBlockKind
    {
        Heading,
        Paragraph,
        BulletedList,
        NumberedList,
        CodeBlock
    }

    public static class ConoscopeHelpTopicIds
    {
        public const string QuickStart = "quick-start";
        public const string CaptureWorkflow = "workflow-capture";
        public const string PreprocessWorkflow = "workflow-preprocess";
        public const string FocusReferenceWorkflow = "workflow-focus-reference";
        public const string GamutWorkflow = "workflow-gamut";
        public const string ContrastWorkflow = "workflow-contrast";
        public const string VisualizationWorkflow = "workflow-visualization";
        public const string FocusPrinciple = "principle-focus-sampling";
        public const string GamutPrinciple = "principle-gamut";
        public const string ContrastPrinciple = "principle-contrast";
        public const string CurvePrinciple = "principle-curves-export";
        public const string VisualizationPrinciple = "principle-visualization";
        public const string ActiveViewTerm = "term-active-view";
        public const string FocusBatchTerm = "term-focus-batch";
        public const string CoordinateTerm = "term-coordinate";
        public const string ChannelTerm = "term-channel";
        public const string GamutCoverageTerm = "term-gamut-coverage";
        public const string ContrastTerm = "term-contrast";
    }

    public sealed class ConoscopeHelpBlock
    {
        public required ConoscopeHelpBlockKind Kind { get; init; }
        public string? Text { get; init; }
        public IReadOnlyList<string>? Items { get; init; }
        public int Level { get; init; }
        public int IndentLevel { get; init; }
    }

    public sealed class ConoscopeHelpEntry
    {
        public required string Id { get; init; }
        public required ConoscopeHelpCategory Category { get; init; }
        public required string Title { get; init; }
        public required string Summary { get; init; }
        public required string Keywords { get; init; }
        public required IReadOnlyList<ConoscopeHelpBlock> DetailBlocks { get; init; }
        public required string SearchText { get; init; }

        public string CategoryDisplay => Category switch
        {
            ConoscopeHelpCategory.QuickStart => "快速开始",
            ConoscopeHelpCategory.Workflow => "使用路径",
            ConoscopeHelpCategory.Principle => "算法原理",
            ConoscopeHelpCategory.Terminology => "名词解析",
            _ => "帮助"
        };
    }

    public static class ConoscopeHelpContent
    {
        private static readonly IReadOnlyList<ConoscopeHelpEntry> Entries = new[]
        {
            Entry(
                ConoscopeHelpTopicIds.QuickStart,
                ConoscopeHelpCategory.QuickStart,
                "快速开始：从打开图像到得到结果",
                "第一次使用 Conoscope 时，建议按主页 -> 关注点 -> 分析结果窗的顺序操作。",
                "快速开始,入门,主页,关注点,分析,色域,对比度,结果窗",
                H("建议按这个顺序使用"),
                Numbers(
                    "打开 工具 -> VAM，进入 Conoscope 主窗口。",
                    "在 主页 页签用打开图像，或在 采集 页签执行流程 / 拍照。",
                    "保证你要操作的标签页是当前活动 View。",
                    "在 主页 -> 当前视图 里先切换显示通道、参考模式和参考值。",
                    "在图像上方工具条里先确认参考拖拽和关注点圆的状态。",
                    "如果只是检查单个点，可对关注点右键直接计算。",
                    "如果要做综合色域或黑白对比度，到 分析 页签记录 R/G/B 或 白/黑。",
                    "结果会在独立结果窗口中显示，不再堆回主界面。"),
                Bullets(1,
                    "先关闭 参考拖拽，避免拖参考线时误操作。",
                    "再开启 关注点圆，左键拖出圆形采样区域。"),
                Bullets(1,
                    "然后点击 计算色域 或 计算对比度。"),
                H("你需要先理解的两个概念"),
                Bullets(
                    "活动 View：主页、分析、导出这些窗口级操作都默认作用于当前活动标签页。",
                    "关注点批次：一组关注点圆会一起被记录成一次测量快照，综合色域和对比度都按这组快照计算。"),
                H("最常见的误区"),
                Bullets(
                    "只切换到别的标签页，却忘了重新确认活动 View，结果把数据记到了别的图上。",
                    "先记录了 R，再修改了关注点位置，然后直接记录 G/B，导致三组数据并不是同一套关注点。",
                    "开着 参考拖拽 就开始画关注点圆，结果拖到的是参考线或极角圆。")),

            Entry(
                ConoscopeHelpTopicIds.CaptureWorkflow,
                ConoscopeHelpCategory.Workflow,
                "采集与视图管理使用路径",
                "采集页负责流程模板、测量相机、ND / 校正绑定，结果默认回到当前活动 View。",
                "采集,流程模板,测量相机,拍照,ND,校正,复用当前视图,活动View",
                H("Ribbon 路径"),
                Bullets(
                    "主页：打开图像、创建空白视图、切换型号、打开观察相机。",
                    "采集 -> 流程采集：选择流程模板、编辑模板、执行流程。",
                    "采集 -> 测量相机：选择相机、选择校正模板、刷新列表、执行拍照。",
                    "采集 -> ND / 校正：设置 ND 端口、读取 ND 端口、绑定 / 解除绑定校正模板。"),
                H("推荐操作顺序"),
                Numbers(
                    "在 主页 确认当前型号和当前活动 View。",
                    "在 采集 -> 流程采集 里选择流程模板。",
                    "如果勾选了 流程/拍图结果复用当前视图，流程结果会直接刷新当前活动 View，而不是新开一页。",
                    "如果要直接拍照，则在 采集 -> 测量相机 里选择相机和校正模板，点击 拍照。",
                    "如果当前相机启用了 ND 口，可在 ND / 校正 分组里把 ND 端口与校正模板绑定，减少重复切换。"),
                H("什么时候用流程，什么时候用拍照"),
                Bullets(
                    "流程：适合已经固定了拍摄和处理步骤的日常重复任务。",
                    "拍照：适合临时查看当前图像、快速调试相机或只想拿一张测量图。"),
                H("结果会去哪"),
                Bullets(
                    "勾选 复用当前视图 时，新的图像会刷新当前活动 View。",
                    "不勾选时，流程或拍照结果可以走独立视图路径，便于对比历史图像。")),

            Entry(
                ConoscopeHelpTopicIds.PreprocessWorkflow,
                ConoscopeHelpCategory.Workflow,
                "预处理与伪彩显示使用路径",
                "预处理页把是否启用、滤波类型、伪彩显示、完整参数设置、应用到当前 View 集中在一页。",
                "预处理,滤波,伪彩,应用当前预设,灰尘滤除,高斯,中值,双边",
                H("Ribbon 路径"),
                Bullets(
                    "预处理 -> 启用：控制是否启用预处理。",
                    "预处理 -> 滤波：选择滤波类型，或打开完整预处理参数窗口。",
                    "预处理 -> 显示：控制是否启用伪彩显示、选择色带并查看预览。",
                    "预处理 -> 应用：把当前预处理预设应用到活动 View。"),
                H("当前界面的职责分工"),
                Bullets(
                    "启用预处理 是总开关。",
                    "滤波类型 负责选择主滤波模式，例如低通、移动平均、高斯、中值、双边。",
                    "设置 会打开完整参数窗口，用于更细的滤波和灰尘滤除配置。",
                    "伪彩显示 只影响显示层，不改变底层 XYZ 数据文件。",
                    "应用 用来立即把当前预设作用到当前活动 View。"),
                H("适合的使用顺序"),
                Numbers(
                    "先在显示通道中确认你要观察的是 Y、x/y/u/v 还是色差。",
                    "再决定是否开启滤波和伪彩。",
                    "对噪声、亮点、局部污染敏感的图像，再打开完整设置窗口微调参数。",
                    "处理后再去画关注点圆或导出曲线，避免前后数据不一致。"),
                H("什么时候不要先预处理"),
                Bullets(
                    "如果你当前想验证原始相机数据是否正常，先不要上滤波。",
                    "如果你已经记录了综合色域或对比度数据，又重新改了预处理，建议重新记录，不要混用不同处理状态下的结果。")),

            Entry(
                ConoscopeHelpTopicIds.FocusReferenceWorkflow,
                ConoscopeHelpCategory.Workflow,
                "关注点圆、参考线与极角圆使用路径",
                "关注点圆在图像上方工具条中创建；参考模式既能在主页调，也能在 View 右侧面板中精调。",
                "关注点圆,参考拖拽,参考线,极角圆,方位角,极坐标,当前视图,右键计算",
                H("入口在哪里"),
                Bullets(
                    "图像上方工具条。",
                    "主页 -> 当前视图：切换显示通道、参考模式和参考值。",
                    "View 右侧 显示 面板：继续微调参考模式、方位角、极角半径。",
                    "View 右下 参考曲线：切换曲线显示为直角坐标或极坐标。"),
                Bullets(1,
                    "参考拖拽：允许直接拖动参考方位线和极角圆。",
                    "关注点圆：开启后左键拖拽绘制圆形关注点，右键可计算或删除。"),
                H("推荐顺序"),
                Numbers(
                    "先在 主页 -> 当前视图 里决定你现在看哪一个显示通道。",
                    "选择参考模式。",
                    "如果要绘制关注点圆，建议先关闭 参考拖拽。",
                    "打开 关注点圆 后，在图像上拖出一个或多个圆。",
                    "如果想验证某个点，直接在关注点上右键计算；如果要做综合色域 / 对比度，再去 分析 页签记录整组关注点。"),
                Bullets(1,
                    "方位角直线：适合做直径方向曲线。",
                    "极角圆：适合做固定半径圆周曲线。"),
                H("和 Engine 里的关注点有什么区别"),
                Bullets(
                    "Conoscope 这里的关注点是插件内部的本地圆形采样逻辑。",
                    "它不会走 Engine 那套带筛除和流程过滤的关注点链路。",
                    "这样做的目的，是让锥光镜用户能在图像上直接、可控地对局部区域取样。")),

            Entry(
                ConoscopeHelpTopicIds.GamutWorkflow,
                ConoscopeHelpCategory.Workflow,
                "综合色域计算使用路径",
                "综合色域现在由主 Ribbon 统一记录 R/G/B 关注点批次，再弹出独立结果窗口显示。",
                "色域,综合色域,RGB,记录R,记录G,记录B,标准色域,覆盖率,CIE",
                H("Ribbon 路径"),
                Bullets(
                    "分析 -> 色域。"),
                Bullets(1,
                    "选择标准色域。",
                    "点击 记录 R / 记录 G / 记录 B。",
                    "点击 计算色域 打开结果窗口。"),
                H("操作步骤"),
                Numbers(
                    "打开 R 图，并确认关注点圆已经放好。",
                    "点击 记录 R。",
                    "切到 G 图，保持同一套关注点，点击 记录 G。",
                    "切到 B 图，点击 记录 B。",
                    "选择要比较的标准色域，例如 sRGB、Display P3、Rec.2020。",
                    "点击 计算色域。"),
                H("结果怎么看"),
                Bullets(
                    "结果窗口会同时给出。",
                    "全部关注点 用于看整体分布。",
                    "选单个关注点时，可以只看某一个位置的实测三角形。"),
                Bullets(1,
                    "每个关注点的 R/G/B 色坐标。",
                    "每个关注点的综合色域覆盖率。",
                    "标准色域和实测色域在 CIE 图中的叠加显示。"),
                H("旧窗口和新流程的区别"),
                Bullets(
                    "窗口 -> 色域窗口 还是保留的旧版手工窗口。",
                    "日常使用建议优先走 分析 -> 色域 的新流程，因为它能直接按当前活动 View 的整组关注点批量计算。")),

            Entry(
                ConoscopeHelpTopicIds.ContrastWorkflow,
                ConoscopeHelpCategory.Workflow,
                "黑白对比度计算使用路径",
                "黑白对比度和综合色域一样，先记录关注点批次，再在独立结果窗口中查看每个点的比值。",
                "对比度,黑白,记录白,记录黑,Y,亮度,结果窗口",
                H("Ribbon 路径"),
                Bullets(
                    "分析 -> 对比度。"),
                Bullets(1,
                    "点击 记录白。",
                    "点击 记录黑。",
                    "点击 计算对比度。"),
                H("操作步骤"),
                Numbers(
                    "打开白场图，在图上确认关注点圆位置。",
                    "点击 记录白。",
                    "打开黑场图，保持同一套关注点位置。",
                    "点击 记录黑。",
                    "点击 计算对比度 打开结果窗口。"),
                H("结果怎么看"),
                Bullets(
                    "每个关注点都会显示。",
                    "汇总区会显示平均值、最小值和最大值。"),
                Bullets(1,
                    "白场亮度 Y。",
                    "黑场亮度 Y。",
                    "对比度比值。"),
                H("注意事项"),
                Bullets(
                    "黑场亮度必须大于 0，否则当前实现会拒绝计算。",
                    "如果你改了关注点位置或预处理状态，请重新记录白 / 黑，不要混用旧数据。",
                    "窗口 -> 对比度窗口 是旧版兼容窗口；常规使用建议走主 分析 页签的新流程。")),

            Entry(
                ConoscopeHelpTopicIds.VisualizationWorkflow,
                ConoscopeHelpCategory.Workflow,
                "CIE、3D、曲线与导出使用路径",
                "CIE 和 3D 是当前活动 View 的可视化工具；曲线和 CSV 导出既能走主分析区，也能走 View 侧导出面板。",
                "CIE,3D,曲线,导出,方位角,极角,高级导出,当前视图",
                H("3D 与 CIE"),
                Bullets(
                    "分析 -> 视图 -> 3D：打开当前图像的 3D 视图。",
                    "分析 -> 视图 -> CIE：打开当前图像的 CIE 色度图窗口。",
                    "图像上方工具条也有 3D 和 CIE 快捷按钮。"),
                H("曲线显示"),
                Bullets(
                    "右侧下方面板是 参考曲线。",
                    "当参考模式是 方位角直线 时，曲线按直径方向显示。",
                    "当参考模式是 极角圆 时，曲线按圆周方向显示。",
                    "极坐标 开关只改变曲线显示方式，不改变底层采样数据。"),
                H("导出路径"),
                Bullets(
                    "分析 -> 导出：方位模式导出、极角模式导出和高级导出。",
                    "View 右侧 导出 页签：选择导出模式、导出通道后执行导出。",
                    "高级导出 适合批量导出多个通道、多个步长或截面数据。"),
                H("使用建议"),
                Bullets(
                    "想看整体地形变化时，先切到合适的显示通道再开 3D。",
                    "想看综合色度位置时，先开 CIE 或直接使用综合色域结果窗中的 CIE 叠图。",
                    "想做后处理分析或和外部脚本联动时，用 CSV 导出而不是截图。")),

            Entry(
                ConoscopeHelpTopicIds.FocusPrinciple,
                ConoscopeHelpCategory.Principle,
                "关注点采样原理",
                "Conoscope 用圆形 ROI 在 XYZ 数据上做均值采样，不走 Engine 的过滤链路。",
                "关注点原理,ROI,圆形采样,XYZ平均,本地计算,批次",
                H("当前实现真正做了什么"),
                Numbers(
                    "用户在图像上画的是一个圆形关注点。",
                    "代码会把这个圆从当前显示位图坐标换算到 XYZ 数据矩阵坐标。",
                    "在换算后的椭圆区域内遍历像素。",
                    "只累加有限值的 X/Y/Z。",
                    "用区域内所有有效像素的均值作为这个关注点的测量值。",
                    "再由均值 X/Y/Z 计算综合色度 x/y/u/v、CCT 等派生量。"),
                H("这套实现的特点"),
                Bullets(
                    "它是圆形区域平均，不是单像素点击。",
                    "它是本地直接采样，不是 Engine 那套可筛除 / 可过滤的 POI 任务。",
                    "同一个关注点还会记录方位角、极角和圆半径对应的角度。"),
                H("为什么这对综合色域 / 对比度重要"),
                Bullets(
                    "色域和对比度都不是拿最后一个点直接算，而是把整组关注点记录成一个批次。",
                    "这样每个点都能在不同图像之间一一对应，得到更稳定的区域比较。")),

            Entry(
                ConoscopeHelpTopicIds.GamutPrinciple,
                ConoscopeHelpCategory.Principle,
                "综合色域计算原理",
                "当前实现把每个关注点的 R/G/B 色度坐标转成三角形，再与标准色域三角形做面积比。",
                "色域原理,xy,三角形面积,覆盖率,标准色域,sRGB,Display P3,Rec2020",
                H("输入数据"),
                Bullets(
                    "R、G、B 三组测量数据来自三个 MeasurementCapture。",
                    "如果是新流程记录，它们通常来自三张图像上的同一套关注点圆。",
                    "每个测量点都已经包含平均 X/Y/Z、x/y/u/v、方位角 / 极角 / 半径角。"),
                H("批处理怎么对齐点位"),
                P("代码优先按下面顺序对齐。"),
                Numbers(
                    "先用关注点 Key 对齐。",
                    "如果没有共享 Key，但多组点数量相同，则按索引对齐。",
                    "如果某一组只有 1 个点，而另一组是多点，该单点会被复用到每个对齐结果里。"),
                P("实际使用时，仍然建议 R/G/B 三次记录使用同一套关注点圆。"),
                H("覆盖率怎么算"),
                Numbers(
                    "取每个关注点的实测 R(x, y)、G(x, y)、B(x, y)。",
                    "把这三个点看成一个三角形。",
                    "用三角形面积公式求出样本面积。",
                    "再用同样的方法算标准色域三角形面积。",
                    "覆盖率 = 样本面积 / 标准面积 * 100%。"),
                Code("Area = |x1(y2-y3) + x2(y3-y1) + x3(y1-y2)| / 2"),
                H("这意味着什么"),
                Bullets(
                    "当前算法比较的是面积比例，不是和标准色域的交集面积。",
                    "所以当实测三角形比标准三角形更大时，结果可能大于 100%。",
                    "结果窗口中的 CIE 图只是显示标准三角形和实测三角形的相对位置，方便人工判断偏移方向。")),

            Entry(
                ConoscopeHelpTopicIds.ContrastPrinciple,
                ConoscopeHelpCategory.Principle,
                "黑白对比度计算原理",
                "当前实现直接使用白场 Y / 黑场 Y，黑场亮度必须大于 0。",
                "对比度原理,Y,亮度,白场,黑场,ratio",
                H("输入数据"),
                Bullets(
                    "白场和黑场各自来自一个 MeasurementCapture。",
                    "每个对齐后的点位都至少包含平均 X/Y/Z。",
                    "当前实现把 Y 当作亮度值使用。"),
                H("公式"),
                Code("Contrast Ratio = Y_white / Y_black"),
                H("计算前的约束"),
                Bullets(
                    "黑场亮度 Y_black 必须大于 0。",
                    "如果黑场亮度小于等于 0，当前实现会直接报错，不继续计算。"),
                H("批处理结果会保留什么"),
                Bullets(
                    "关注点编号和名称。",
                    "方位角 / 极角 / 半径角。",
                    "白场亮度、黑场亮度。",
                    "对比度比值。",
                    "白图和黑图各自的综合色度坐标。"),
                H("应怎样理解结果"),
                Bullets(
                    "比值越大，表示白 / 黑分离越明显。",
                    "如果多个点差异很大，先回头看是否是图像边缘、局部杂散光、预处理状态不一致，或关注点没放在同一物理区域上。")),

            Entry(
                ConoscopeHelpTopicIds.CurvePrinciple,
                ConoscopeHelpCategory.Principle,
                "参考曲线与导出原理",
                "方位角模式沿直径采样，极角模式沿固定半径圆周采样，导出通道由当前选择决定。",
                "曲线原理,导出原理,方位角,极角,CSV,截面,高级导出",
                H("方位角直线模式"),
                Bullets(
                    "曲线来源于一条穿过图像的参考直线。",
                    "采样点数量约等于这条线在图像上的像素长度。",
                    "曲线横轴会映射到 -MaxAngle ~ +MaxAngle 的角度范围。"),
                H("极角圆模式"),
                Bullets(
                    "曲线来源于固定半径的一条圆周。",
                    "当前实现按 360 个等角度采样点围一圈取样。",
                    "横轴对应 0° 到 360° 的圆周角度位置。"),
                H("导出时会带什么通道"),
                Bullets(
                    "X",
                    "Y",
                    "Z",
                    "CIE x",
                    "CIE y",
                    "CIE u",
                    "CIE v",
                    "色差 Δuv"),
                H("高级导出还能做什么"),
                Bullets(
                    "按步长批量导出方位角或极角数据。",
                    "只导出某一个截面。",
                    "在一个文件夹里一次导出多个通道。"),
                H("什么时候用哪一种"),
                Bullets(
                    "想看穿过中心的分布变化，用 方位角直线。",
                    "想看某个半径上的环形均匀性，用 极角圆。",
                    "想交给外部脚本、Excel 或 Python 处理时，用 CSV 导出。")),

            Entry(
                ConoscopeHelpTopicIds.VisualizationPrinciple,
                ConoscopeHelpCategory.Principle,
                "CIE 与 3D 可视化原理",
                "3D 使用当前显示通道生成高度图；CIE 有当前视图查看和结果窗叠图两种入口。",
                "CIE原理,3D原理,高度图,显示通道,综合色度,结果窗",
                H("3D 当前是怎么来的"),
                Bullets(
                    "3D 不是单独重新计算一份图像。",
                    "当前实现直接把 XMat / YMat / ZMat 和当前选中的显示通道交给高度图渲染器。",
                    "所以你在进入 3D 前选择的是 Y、x、u 还是 色差 Δuv，会直接影响 3D 高度表现。"),
                H("CIE 有两种入口"),
                H("1. 当前视图的 CIE 工具", 3),
                Bullets(
                    "来自图像工具条或 分析 -> 视图 -> CIE。",
                    "用来查看当前图像上的综合色度位置。",
                    "它更偏向当前图像可视化，不是综合色域批处理结果窗口。"),
                H("2. 综合色域结果窗里的 CIE 叠图", 3),
                Bullets(
                    "来自 计算色域 后弹出的结果窗口。",
                    "会叠加标准色域三角形、实测色域三角形和 R/G/B 标记点。",
                    "这个入口更适合比较不同关注点与标准色域的偏差。"),
                H("如何选用"),
                Bullets(
                    "想看当前图像某个区域的综合色度位置，先用当前视图的 CIE。",
                    "想看综合色域覆盖率和标准三角形对比，直接看综合色域结果窗。",
                    "想看空间起伏或局部高低变化，再配合 3D。")),

            Entry(
                ConoscopeHelpTopicIds.ActiveViewTerm,
                ConoscopeHelpCategory.Terminology,
                "活动 View",
                "当前活动 View 就是当前被选中的图像标签页，主页和分析页的大多数按钮都对它生效。",
                "活动View,当前视图,标签页,ActiveView",
                H("定义"),
                P("活动 View 指当前被选中的 Conoscope 图像标签页。"),
                H("为什么它很重要"),
                P("下面这些窗口级操作都默认作用于活动 View。"),
                Bullets(
                    "主页中的当前视图快捷控制。",
                    "预处理 应用 按钮。",
                    "分析页的 3D / CIE / 导出。",
                    "分析页的 R/G/B、白/黑记录。"),
                H("典型误区"),
                Bullets(
                    "视觉上以为自己在看 A 图，但当前被激活的其实是 B 图。",
                    "先画完关注点后切了标签页，再去点 记录 R，结果记录到了别的 View。")),

            Entry(
                ConoscopeHelpTopicIds.FocusBatchTerm,
                ConoscopeHelpCategory.Terminology,
                "关注点与关注点批次",
                "一个关注点是一个圆形采样区；一个关注点批次是当前 View 上整组关注点的一次统一记录。",
                "关注点,批次,FocusPoint,MeasurementCapture,ROI",
                H("关注点"),
                Bullets(
                    "关注点是画在图上的一个圆形 ROI。",
                    "它代表对这块区域做平均采样，而不是单像素取值。"),
                H("关注点批次"),
                Bullets(
                    "当你点击 记录 R、记录 G、记录白 这类按钮时，系统会把当前 View 上的全部关注点一次性保存下来。",
                    "这次保存就是一个批次，也就是 MeasurementCapture。"),
                H("为什么要按批次记录"),
                Bullets(
                    "因为综合色域和对比度都需要不同图像上的同一组位置做对比。",
                    "只有整组点一起记录，后续才能对齐并逐点计算。")),

            Entry(
                ConoscopeHelpTopicIds.CoordinateTerm,
                ConoscopeHelpCategory.Terminology,
                "方位角、极角与半径角",
                "方位角决定方向，极角圆决定离中心多远，半径角描述关注点圆本身的角度尺度。",
                "方位角,极角,半径角,参考线,参考圆",
                H("方位角"),
                Bullets(
                    "用来描述参考直线的方向。",
                    "在 方位角直线 模式下，参考曲线通常沿这条线采样。"),
                H("极角"),
                Bullets(
                    "用来描述参考圆离图像中心的角度距离。",
                    "在 极角圆 模式下，参考曲线会沿这个半径对应的圆周采样。"),
                H("半径角"),
                Bullets(
                    "在关注点语境里，半径角描述的是这个圆形关注点有多大。",
                    "它和参考极角不是一回事。"),
                Bullets(1,
                    "参考极角是参考圆位置。",
                    "半径角是关注点大小。")),

            Entry(
                ConoscopeHelpTopicIds.ChannelTerm,
                ConoscopeHelpCategory.Terminology,
                "显示通道与 CIE x/y/u/v",
                "X/Y/Z 是基础三刺激值，x/y/u/v 是综合色度坐标；3D 和显示都会受当前通道选择影响。",
                "显示通道,X,Y,Z,x,y,u,v,CIE,色差,Δuv",
                H("X / Y / Z"),
                Bullets(
                    "这是图像的基础三刺激值。",
                    "其中 Y 在当前实现里也被当作亮度使用，因此黑白对比度是基于 Y 来算的。"),
                H("x / y / u / v"),
                Bullets(
                    "这是综合色度坐标。",
                    "综合色域计算里，实际参与面积计算的是 x/y。",
                    "CIE 显示与综合色度比较时，也主要围绕这些坐标展开。"),
                H("色差 Δuv"),
                Bullets(
                    "色差通道用于显示与当前参考的综合色差异。",
                    "它适合看偏色分布，但不直接参与综合色域面积计算。"),
                H("一个容易忽略的点"),
                Bullets(
                    "当前选中的显示通道不仅影响图像显示，也会影响 3D 高度图表现。")),

            Entry(
                ConoscopeHelpTopicIds.GamutCoverageTerm,
                ConoscopeHelpCategory.Terminology,
                "样本面积、标准面积与覆盖率",
                "当前覆盖率是面积比，不是和标准色域求交集之后的面积比例。",
                "样本面积,标准面积,覆盖率,色域覆盖,面积比",
                H("样本面积"),
                Bullets(
                    "指实测 R/G/B 三个点在 x/y 平面围成的三角形面积。"),
                H("标准面积"),
                Bullets(
                    "指所选标准色域，例如 sRGB、Display P3、Rec.2020 的标准三角形面积。"),
                H("覆盖率"),
                P("当前实现。"),
                Code("覆盖率 = 样本面积 / 标准面积 × 100%"),
                H("为什么这要单独强调"),
                Bullets(
                    "这个定义计算简单、结果稳定，适合当前批量关注点流程。",
                    "但它不是实测三角形和标准三角形重叠部分的面积比。",
                    "所以当实测三角形比标准更大时，数值可能超过 100%。")),

            Entry(
                ConoscopeHelpTopicIds.ContrastTerm,
                ConoscopeHelpCategory.Terminology,
                "白场、黑场与对比度",
                "白场和黑场是同一位置在不同状态下的亮度比较，对比度直接取白 Y / 黑 Y。",
                "白场,黑场,对比度,Y,亮度,ratio",
                H("白场"),
                Bullets(
                    "指显示或样品处于白状态时的测量结果。",
                    "当前帮助语境里，主要关心其 Y 值。"),
                H("黑场"),
                Bullets(
                    "指显示或样品处于黑状态时的测量结果。",
                    "黑场 Y 越低，通常越有利于得到更高的对比度。"),
                H("对比度"),
                P("当前实现直接定义为。"),
                Code("Contrast Ratio = Y_white / Y_black"),
                H("为什么要用同一套关注点"),
                Bullets(
                    "因为对比度比较的是同一物理区域在白态和黑态下的亮度变化。",
                    "如果白图和黑图的关注点不对应，算出来的对比度就没有可比性。"))
        };

        public static IReadOnlyList<ConoscopeHelpEntry> GetAllEntries()
        {
            return Entries;
        }

        private static ConoscopeHelpEntry Entry(
            string id,
            ConoscopeHelpCategory category,
            string title,
            string summary,
            string keywords,
            params ConoscopeHelpBlock[] blocks)
        {
            return new ConoscopeHelpEntry
            {
                Id = id,
                Category = category,
                Title = title,
                Summary = summary,
                Keywords = keywords,
                DetailBlocks = blocks,
                SearchText = BuildSearchText(title, summary, keywords, blocks)
            };
        }

        private static ConoscopeHelpBlock H(string text, int level = 2)
        {
            return new ConoscopeHelpBlock
            {
                Kind = ConoscopeHelpBlockKind.Heading,
                Text = text,
                Level = level
            };
        }

        private static ConoscopeHelpBlock P(string text)
        {
            return new ConoscopeHelpBlock
            {
                Kind = ConoscopeHelpBlockKind.Paragraph,
                Text = text
            };
        }

        private static ConoscopeHelpBlock Bullets(params string[] items)
        {
            return Bullets(0, items);
        }

        private static ConoscopeHelpBlock Bullets(int indentLevel, params string[] items)
        {
            return new ConoscopeHelpBlock
            {
                Kind = ConoscopeHelpBlockKind.BulletedList,
                Items = items,
                IndentLevel = indentLevel
            };
        }

        private static ConoscopeHelpBlock Numbers(params string[] items)
        {
            return Numbers(0, items);
        }

        private static ConoscopeHelpBlock Numbers(int indentLevel, params string[] items)
        {
            return new ConoscopeHelpBlock
            {
                Kind = ConoscopeHelpBlockKind.NumberedList,
                Items = items,
                IndentLevel = indentLevel
            };
        }

        private static ConoscopeHelpBlock Code(string text)
        {
            return new ConoscopeHelpBlock
            {
                Kind = ConoscopeHelpBlockKind.CodeBlock,
                Text = text
            };
        }

        private static string BuildSearchText(
            string title,
            string summary,
            string keywords,
            IReadOnlyList<ConoscopeHelpBlock> blocks)
        {
            StringBuilder builder = new();
            builder.Append(title).Append(' ')
                .Append(summary).Append(' ')
                .Append(keywords).Append(' ');

            foreach (ConoscopeHelpBlock block in blocks)
            {
                if (!string.IsNullOrWhiteSpace(block.Text))
                {
                    builder.Append(block.Text).Append(' ');
                }

                if (block.Items == null)
                {
                    continue;
                }

                foreach (string item in block.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        builder.Append(item).Append(' ');
                    }
                }
            }

            return builder.ToString();
        }
    }
}
