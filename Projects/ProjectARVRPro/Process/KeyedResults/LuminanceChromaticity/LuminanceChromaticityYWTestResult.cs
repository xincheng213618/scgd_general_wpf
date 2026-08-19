using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    public class LuminanceChromaticityYWViewTestResult : LuminanceChromaticityYWTestResult
    {
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas12X7 { get; set; } = new();
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas8X7 { get; set; } = new();
    }

    public class LuminanceChromaticityYWTestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas12X7 { get; set; } = new();
        public ObjectiveTestItem AverageLuminance12X7 { get; set; } = new();
        public ObjectiveTestItem LuminanceUniformity12X7 { get; set; } = new();
        public ObjectiveTestItem ColorUniformity12X7 { get; set; } = new();

        public List<PoixyuvData> PoixyuvDatas8X7 { get; set; } = new();
        public ObjectiveTestItem AverageLuminance8X7 { get; set; } = new();
        public ObjectiveTestItem LuminanceUniformity8X7 { get; set; } = new();
        public ObjectiveTestItem ColorUniformity8X7 { get; set; } = new();
    }
}
