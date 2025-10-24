using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectLUX.Process.W255
{
    public class W255ViewTestResult : W255TestResult
    {
        public List<PoiResultCIExyuvData> PoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }

    public class W255TestResult : ViewModelBase
    {
        /// <summary>
        /// 亮度均匀性(%) 测试项
        /// </summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// 色彩均匀性 测试项
        /// </summary>
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// 中心点亮度
        /// </summary>
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem();
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesx
        /// </summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem();
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesy
        /// </summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem();
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesu
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem();
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesv
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem();


        /// <summary>
        /// 水平视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; }

        /// <summary>
        /// 垂直视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; }

        /// <summary>
        /// 对角线视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; }
    }
}
