using System.Collections.Generic;

namespace ProjectARVRPro.Process.ScreenDefects
{
    /// <summary>
    /// 单个屏幕缺陷框及其可选测量值。
    /// </summary>
    public sealed class ScreenDefectData
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Area { get; set; }
        public double? Contrast { get; set; }
        public double? MeanValue { get; set; }
        public double? LocalMean { get; set; }
    }

    /// <summary>
    /// 屏幕缺陷检测的汇总信息及缺陷框列表。
    /// </summary>
    public sealed class ScreenDefectsData
    {
        public double? AvgBrightness { get; set; }
        public int DefectCount { get; set; }
        public string GradeLevel { get; set; }
        public string TimeStamp { get; set; }
        public List<ScreenDefectData> Defects { get; set; } = new List<ScreenDefectData>();
    }
}
