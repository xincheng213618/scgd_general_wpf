using ColorVision.Common.MVVM;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    /// <summary>
    /// 按配置名称输出的亮度、色度及均匀性测试结果。
    /// </summary>
    public class LuminanceChromaticityTestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem CenterLuminance { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem();
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem();
    }
}
