using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectARVRPro.Process.W25
{
    public class W25ViewTestResult : W25TestResult
    {
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }


    public class W25TestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();

        /// <summary>
        /// 中心点亮度
        /// </summary>
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem() { Name = "Center_Lunimance ", Unit = "cd/m^2" };
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesx
        /// </summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_x" };
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesy
        /// </summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_y" };
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesu
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1976Chromatic_Coordinates_u'" };
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesv
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1976Chromatic_Coordinates_v'" };
    }
}
