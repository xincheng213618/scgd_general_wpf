using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectARVRPro.Process.RGB.LuminanceChromaticity
{
    public class LuminanceChromaticityViewTestResult : LuminanceChromaticityTestResult
    {
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas { get; set; } = new();
    }

    public class LuminanceChromaticityTestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new();
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new();
        public ObjectiveTestItem ColorUniformity { get; set; } = new();
        public ObjectiveTestItem CenterLuminance { get; set; } = new();
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new();
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new();
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new();
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new();
    }
}
