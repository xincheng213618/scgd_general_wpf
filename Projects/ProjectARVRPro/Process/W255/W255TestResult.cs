using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectARVRPro.Process.W255
{
    public class W255ViewTestResult : W255TestResult
    {
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }


    public class PoixyuvData
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public double CCT { get; set; }

        public double Wave { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double u { get; set; }


        public double v { get; set; }

        public double x { get; set; }


        public double y { get; set; }

    }

    public class W255TestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();

        /// <summary>
        /// 水平视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Horizontal_Field_Of_View_Angle" ,Unit = "degree" };

        /// <summary>
        /// 垂直视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        /// <summary>
        /// 对角线视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };

        /// <summary>
        /// 亮度均匀性(%) 测试项
        /// </summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem() { Name = "Luminance_Uniformity(min/max*100%)" };

        /// <summary>
        /// 色彩均匀性 测试项
        /// </summary>
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem() { Name = "Color_Uniformity(△u'v'max)" };

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
