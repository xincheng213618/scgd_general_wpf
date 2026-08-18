using ColorVision.Common.MVVM;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    /// <summary>
    /// YW 12X7 与 8X7 两组POI的亮色度统计结果。
    /// </summary>
    public class LuminanceChromaticityYWTestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas12X7 { get; set; } = new List<PoixyuvData>();
        public ObjectiveTestItem AverageLuminance12X7 { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem LuminanceUniformity12X7 { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem ColorUniformity12X7 { get; set; } = new ObjectiveTestItem();

        public List<PoixyuvData> PoixyuvDatas8X7 { get; set; } = new List<PoixyuvData>();
        public ObjectiveTestItem AverageLuminance8X7 { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem LuminanceUniformity8X7 { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem ColorUniformity8X7 { get; set; } = new ObjectiveTestItem();
    }
}
