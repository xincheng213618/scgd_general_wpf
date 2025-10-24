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
        /// 水平视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Horizontal_Field_Of_View_Angle" , Unit = "degree" };

        /// <summary>
        /// 垂直视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        /// <summary>
        /// 对角线视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };

        public ObjectiveTestItem P1Lv { get; set; } = new ObjectiveTestItem() { Name = "P1(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P1Cx { get; set; } = new ObjectiveTestItem() { Name = "P1(Cx)" };
        public ObjectiveTestItem P1Cy { get; set; } = new ObjectiveTestItem() { Name = "P1(Cy)"};
        public ObjectiveTestItem P1Cu { get; set; } = new ObjectiveTestItem() { Name = "P1(u')" };
        public ObjectiveTestItem P1Cv { get; set; } = new ObjectiveTestItem() { Name = "P1(v')" };
        public ObjectiveTestItem P2Lv { get; set; } = new ObjectiveTestItem() { Name = "P2(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P2Cx { get; set; } = new ObjectiveTestItem() { Name = "P2(Cx)" };
        public ObjectiveTestItem P2Cy { get; set; } = new ObjectiveTestItem() { Name = "P2(Cy)" };
        public ObjectiveTestItem P2Cu { get; set; } = new ObjectiveTestItem() { Name = "P2(u')" };
        public ObjectiveTestItem P2Cv { get; set; } = new ObjectiveTestItem() { Name = "P2(v')" };
        public ObjectiveTestItem P3Lv { get; set; } = new ObjectiveTestItem() { Name = "P3(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P3Cx { get; set; } = new ObjectiveTestItem() { Name = "P3(Cx)" };
        public ObjectiveTestItem P3Cy { get; set; } = new ObjectiveTestItem() { Name = "P3(Cy)" };
        public ObjectiveTestItem P3Cu { get; set; } = new ObjectiveTestItem() { Name = "P3(u')" };
        public ObjectiveTestItem P3Cv { get; set; } = new ObjectiveTestItem() { Name = "P3(v')" };
        public ObjectiveTestItem P4Lv { get; set; } = new ObjectiveTestItem() { Name = "P4(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P4Cx { get; set; } = new ObjectiveTestItem() { Name = "P4(Cx)" };
        public ObjectiveTestItem P4Cy { get; set; } = new ObjectiveTestItem() { Name = "P4(Cy)" };
        public ObjectiveTestItem P4Cu { get; set; } = new ObjectiveTestItem() { Name = "P4(u')" };
        public ObjectiveTestItem P4Cv { get; set; } = new ObjectiveTestItem() { Name = "P4(v')" };
        public ObjectiveTestItem P5Lv { get; set; } = new ObjectiveTestItem() { Name = "P5(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P5Cx { get; set; } = new ObjectiveTestItem() { Name = "P5(Cx)" };
        public ObjectiveTestItem P5Cy { get; set; } = new ObjectiveTestItem() { Name = "P5(Cy)" };
        public ObjectiveTestItem P5Cu { get; set; } = new ObjectiveTestItem() { Name = "P5(u')" };
        public ObjectiveTestItem P5Cv { get; set; } = new ObjectiveTestItem() { Name = "P5(v')" };
        public ObjectiveTestItem P6Lv { get; set; } = new ObjectiveTestItem() { Name = "P6(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P6Cx { get; set; } = new ObjectiveTestItem() { Name = "P6(Cx)" };
        public ObjectiveTestItem P6Cy { get; set; } = new ObjectiveTestItem() { Name = "P6(Cy)" };
        public ObjectiveTestItem P6Cu { get; set; } = new ObjectiveTestItem() { Name = "P6(u')" };
        public ObjectiveTestItem P6Cv { get; set; } = new ObjectiveTestItem() { Name = "P6(v')" };
        public ObjectiveTestItem P7Lv { get; set; } = new ObjectiveTestItem() { Name = "P7(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P7Cx { get; set; } = new ObjectiveTestItem() { Name = "P7(Cx)" };
        public ObjectiveTestItem P7Cy { get; set; } = new ObjectiveTestItem() { Name = "P7(Cy)" };
        public ObjectiveTestItem P7Cu { get; set; } = new ObjectiveTestItem() { Name = "P7(u')" };
        public ObjectiveTestItem P7Cv { get; set; } = new ObjectiveTestItem() { Name = "P7(v')" };
        public ObjectiveTestItem P8Lv { get; set; } = new ObjectiveTestItem() { Name = "P8(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P8Cx { get; set; } = new ObjectiveTestItem() { Name = "P8(Cx)" };
        public ObjectiveTestItem P8Cy { get; set; } = new ObjectiveTestItem() { Name = "P8(Cy)" };
        public ObjectiveTestItem P8Cu { get; set; } = new ObjectiveTestItem() { Name = "P8(u')" };
        public ObjectiveTestItem P8Cv { get; set; } = new ObjectiveTestItem() { Name = "P8(v')" };
        public ObjectiveTestItem P9Lv { get; set; } = new ObjectiveTestItem() { Name = "P9(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P9Cx { get; set; } = new ObjectiveTestItem() { Name = "P9(Cx)" };
        public ObjectiveTestItem P9Cy { get; set; } = new ObjectiveTestItem() { Name = "P9(Cy)" };
        public ObjectiveTestItem P9Cu { get; set; } = new ObjectiveTestItem() { Name = "P9(u')" };
        public ObjectiveTestItem P9Cv { get; set; } = new ObjectiveTestItem() { Name = "P9(v')" };


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
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem() { Name = "Center_Lunimance " ,Unit = "cd/m^2" };
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
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_u'" };
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesv
        /// </summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_v'" };
    }
}
