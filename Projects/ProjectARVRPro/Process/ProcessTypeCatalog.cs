using ProjectARVRPro.Process.AOI;
using ProjectARVRPro.Process.Blank;
using ProjectARVRPro.Process.Demura;
using ProjectARVRPro.Process.DemuraAOI;

namespace ProjectARVRPro.Process
{
    public sealed class ProcessTypeOption
    {
        public required IProcess Process { get; init; }
        public required string Category { get; init; }
        public required string Subcategory { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
        public required string Capabilities { get; init; }

        public string TypeName => Process.GetType().Name;
        public string FullTypeName => Process.GetType().FullName ?? TypeName;
        public string ConfigurationSummary =>
            Process.GetProcessConfig() != null && Process.GetRecipeConfig() != null ? "支持 Process 与 Recipe 配置" :
            Process.GetProcessConfig() != null ? "支持 Process 配置" :
            Process.GetRecipeConfig() != null ? "支持 Recipe 配置" :
            "无需额外配置";
    }

    public static class ProcessTypeCatalog
    {
        public const string ArvrCategory = "ARVR";
        public const string BlankCategory = "空模板";
        public const string AoiCategory = "AOI";
        public const string DemuraCategory = "Demura";
        public const string MtfSubcategory = "MTF";
        public const string ChessboardSubcategory = "棋盘格";
        public const string LuminanceSubcategory = "亮度与色度";
        public const string GeometrySubcategory = "光学与几何";
        public const string DefectSubcategory = "缺陷检测";
        public const string OtherSubcategory = "其他";

        public static IReadOnlyList<ProcessTypeOption> CreateOptions(IEnumerable<IProcess> processes)
        {
            ArgumentNullException.ThrowIfNull(processes);

            return processes
                .Where(process => process is not BlankProcess)
                .GroupBy(process => process.GetType().FullName, StringComparer.Ordinal)
                .Select(group => CreateOption(group.First()))
                .OrderBy(option => GetCategoryOrder(option.Category))
                .ThenBy(option => GetSubcategoryOrder(option.Subcategory))
                .ThenBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static string GetCategory(Type processType)
        {
            ArgumentNullException.ThrowIfNull(processType);

            if (typeof(BlankProcess).IsAssignableFrom(processType))
                return BlankCategory;

            if (typeof(AOIProcess).IsAssignableFrom(processType))
                return AoiCategory;

            if (typeof(DemuraProcess).IsAssignableFrom(processType)
                || typeof(DemuraAoiProcess).IsAssignableFrom(processType))
                return DemuraCategory;

            return ArvrCategory;
        }

        public static bool IsBlankProcess(IProcess? process) => process is null or BlankProcess;

        public static string GetSubcategory(Type processType)
        {
            ArgumentNullException.ThrowIfNull(processType);

            string category = GetCategory(processType);
            if (category != ArvrCategory)
                return category;

            string typeName = processType.Name;
            if (typeName.StartsWith("MTF", StringComparison.Ordinal))
                return MtfSubcategory;

            if (typeName.StartsWith("Chessboard", StringComparison.Ordinal))
                return ChessboardSubcategory;

            if (typeName is "BlackProcess"
                or "LuminanceChromaticityProcess"
                or "PoiDynamicProcess"
                or "White51Process"
                or "White255Process")
            {
                return LuminanceSubcategory;
            }

            if (typeName.StartsWith("Distortion", StringComparison.Ordinal)
                || typeName.StartsWith("OpticCenter", StringComparison.Ordinal)
                || typeName == "FieldOfViewProcess")
            {
                return GeometrySubcategory;
            }

            if (typeName == "DetectScreenDefectsProcess")
                return DefectSubcategory;

            return OtherSubcategory;
        }

        private static ProcessTypeOption CreateOption(IProcess process)
        {
            string typeName = process.GetType().Name;
            var (displayName, description, capabilities) = GetDocumentation(typeName);
            return new ProcessTypeOption
            {
                Process = process,
                Category = GetCategory(process.GetType()),
                Subcategory = GetSubcategory(process.GetType()),
                DisplayName = displayName,
                Description = description,
                Capabilities = capabilities
            };
        }

        private static int GetCategoryOrder(string category) => category switch
        {
            ArvrCategory => 0,
            BlankCategory => 1,
            AoiCategory => 2,
            DemuraCategory => 3,
            _ => 4
        };

        private static int GetSubcategoryOrder(string subcategory) => subcategory switch
        {
            MtfSubcategory => 0,
            ChessboardSubcategory => 1,
            LuminanceSubcategory => 2,
            GeometrySubcategory => 3,
            DefectSubcategory => 4,
            OtherSubcategory => 5,
            _ => 6
        };

        private static (string DisplayName, string Description, string Capabilities) GetDocumentation(string typeName) => typeName switch
        {
            "BlackProcess" => ("黑画面", "解析黑画面测量结果，用于暗态亮度、漏光与黑画面区域判定。", "结果解析 · 阈值判定 · 区域叠加 · 文本结果"),
            "ChessboardProcess" => ("棋盘格", "解析静态棋盘格结果，用于对比度及棋盘格亮暗区域评价。", "对比度 · 阈值判定 · 点位叠加 · 文本结果"),
            "ChessboardDynamicProcess" => ("动态棋盘格", "解析动态棋盘格结果，适合运行时生成点位的棋盘格对比度评价。", "动态点位 · 对比度 · 阈值判定 · 图形叠加"),
            "DistortionProcess" => ("畸变", "解析固定点位的畸变测量结果，计算并显示几何畸变数据。", "畸变计算 · 阈值判定 · 点位叠加 · 文本结果"),
            "DistortionDynamicProcess" => ("动态畸变", "解析动态点位的畸变测量结果，适配点位数量或布局变化的测试。", "动态点位 · 畸变计算 · 图形叠加 · 文本结果"),
            "FieldOfViewProcess" => ("视场角", "解析视场角关键结果，计算水平、垂直及对角视场并显示边界。", "FOV 计算 · 阈值判定 · 边界叠加 · 结构化结果"),
            "LuminanceChromaticityProcess" => ("亮色度", "解析关键点亮度与色度结果，用于亮度、色坐标及均匀性评价。", "亮度 · 色度 · 均匀性 · 点位叠加"),
            "MTFProcess" => ("MTF 通用", "解析通用 MTF 结果，按测量点输出清晰度评价。", "MTF 解析 · 阈值判定 · 区域叠加 · 文本结果"),
            "MTFHProcess" => ("MTF-H", "解析水平方向 MTF 结果，并按测量区域完成阈值评价。", "水平 MTF · 阈值判定 · 区域叠加 · 文本结果"),
            "MTFVProcess" => ("MTF-V", "解析垂直方向 MTF 结果，并按测量区域完成阈值评价。", "垂直 MTF · 阈值判定 · 区域叠加 · 文本结果"),
            "MTFHVProcess" => ("MTF-HV", "同时解析水平与垂直 MTF 结果，适合常规双方向清晰度测试。", "水平/垂直 MTF · 阈值判定 · 区域叠加"),
            "MTFHVDynamicProcess" => ("动态 MTF-HV", "解析动态区域的水平与垂直 MTF，适配测量区域变化的流程。", "动态区域 · 水平/垂直 MTF · 阈值判定 · 图形叠加"),
            "MTFHV048Process" => ("MTF-HV 0.48", "解析 0.48 规格的水平与垂直 MTF 结果。", "0.48 规格 · 水平/垂直 MTF · 阈值判定 · 区域叠加"),
            "MTFHV058Process" => ("MTF-HV 0.58", "解析 0.58 规格的水平与垂直 MTF 结果。", "0.58 规格 · 水平/垂直 MTF · 阈值判定 · 区域叠加"),
            "OpticCenterProcess" => ("光学中心", "解析固定点位光学中心结果，用于中心偏移及位置评价。", "中心定位 · 偏移评价 · 阈值判定 · 文本结果"),
            "OpticCenterDynamicProcess" => ("动态光学中心", "解析动态点位光学中心结果，适配运行时变化的中心定位流程。", "动态点位 · 中心定位 · 偏移评价 · 文本结果"),
            "PoiDynamicProcess" => ("动态 POI", "解析动态 POI 测量结果，用于运行时生成点位的亮色度展示。", "动态点位 · POI 解析 · 图形叠加 · 文本结果"),
            "DetectScreenDefectsProcess" => ("屏幕缺陷", "解析屏幕缺陷算法结果，显示缺陷类型、位置、尺寸及缺陷区域。", "缺陷解析 · 缺陷列表 · 区域叠加 · 结构化结果"),
            "White51Process" => ("White 51", "解析 White 51 点位结果，用于白画面亮度、色度与均匀性评价。", "51 点位 · 亮色度 · 均匀性 · 边界叠加"),
            "White255Process" => ("White 255", "解析 White 255 点位结果，用于高密度白画面亮色度及均匀性评价。", "255 点位 · 亮色度 · 均匀性 · 点位叠加"),
            "AOIProcess" => ("OLED AOI", "解析 OLED AOI 检测结果，用于画面异常、缺陷与检测状态的综合评价。", "AOI 结果解析 · 缺陷判定 · 图形展示 · 结构化结果"),
            "DemuraProcess" => ("Demura", "执行并解析 Demura 补偿流程，包括测量数据准备、补偿文件生成与设备交互。", "数据准备 · 补偿文件生成/合并 · 设备通信 · 统计结果"),
            "DemuraAoiProcess" => ("Demura AOI", "解析 Demura AOI 检测结果并按配方完成缺陷评价与结构化输出。", "Demura 缺陷解析 · 配方判定 · 文本结果 · 结构化结果"),
            _ => (typeName, $"使用 {typeName} 解析对应流程结果。", "结果解析 · 结果展示")
        };
    }
}
