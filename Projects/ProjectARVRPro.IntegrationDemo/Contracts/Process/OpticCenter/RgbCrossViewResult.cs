using ProjectARVRPro.Recipe;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.OpticCenter
{
    /// <summary>原图像素坐标中的矩形。</summary>
    public sealed class RgbCrossRectangle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>一个颜色通道的水平臂和垂直臂边缘框。</summary>
    public sealed class RgbCrossChannel
    {
        public string Name { get; set; } = string.Empty;
        public RgbCrossRectangle Horizontal { get; set; }
        public RgbCrossRectangle Vertical { get; set; }
    }

    /// <summary>指定颜色通道相对 G 基准通道的最大边缘偏移及判定。</summary>
    public sealed class RgbCrossComparison
    {
        public double? Value { get; set; }
        public double? JudgedValue { get; set; }
        public string Judgment { get; set; } = "MEASURED";
    }

    /// <summary>一个十字点位的 RGB 边缘、分离值和判定。</summary>
    public sealed class RgbCrossPoint
    {
        public string Id { get; set; } = string.Empty;
        public bool Valid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public RgbCrossRectangle Region { get; set; }
        public List<RgbCrossChannel> Channels { get; set; } = new List<RgbCrossChannel>();
        public Dictionary<string, RgbCrossComparison> Comparisons { get; set; } = new Dictionary<string, RgbCrossComparison>();
        public double? MaximumEdgeSeparation { get; set; }
        public double? JudgedEdgeSeparation { get; set; }
        public string Judgment { get; set; } = "MEASURED";
    }

    /// <summary>
    /// 十字 RGB 分离的当前公开结果模型。Comparisons 的标准 Key 为 R-G 和 B-G。
    /// </summary>
    public sealed class RgbCrossViewResult
    {
        public int GridRows { get; set; } = 3;
        public int GridColumns { get; set; } = 3;
        public string ExportName { get; set; } = "RgbCross";
        public int? SourceMasterId { get; set; }
        public string MeasurementId { get; set; } = string.Empty;
        public string AlgorithmVersion { get; set; } = string.Empty;
        public string SourceImageId { get; set; } = string.Empty;
        public string SourceSha256 { get; set; }
        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }
        public string JsonFile { get; set; } = string.Empty;
        public string JsonSha256 { get; set; } = string.Empty;
        public string SourceImageFile { get; set; } = string.Empty;
        public RecipeBase AppliedRecipe { get; set; }
        public string Status { get; set; } = "MEASURED";
        public string Error { get; set; } = string.Empty;
        public List<RgbCrossPoint> Points { get; set; } = new List<RgbCrossPoint>();
    }
}
