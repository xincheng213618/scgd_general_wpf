using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.W255
{
    /// <summary>
    /// W255 白场 255 阶综合测试结果，包含视场角、均匀性、中心亮度和色品坐标。
    /// </summary>
    public class W255TestResult : ViewModelBase
    {
        /// <summary>测点光色数据列表，包含亮度、色品坐标、色温等原始测点结果。</summary>
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();
        /// <summary>水平视场角，表示画面水平方向可观察范围，单位 degree。</summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem { Name = "Horizontal_Field_Of_View_Angle", Unit = "degree" };
        /// <summary>垂直视场角，表示画面垂直方向可观察范围，单位 degree。</summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        /// <summary>对角线视场角，表示画面对角方向可观察范围，单位 degree。</summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };
        /// <summary>亮度均匀性，通常按最小亮度/最大亮度*100% 计算，单位 %，越高越均匀。</summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem { Name = "Luminance_Uniformity(min/max*100%)", Unit = "%" };
        /// <summary>色度均匀性，通常取最大 Delta u'v'，越小越均匀。</summary>
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem { Name = "Color_Uniformity(Δu'v'max)" };
        /// <summary>中心相关色温 CCT，单位 K。</summary>
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; } = new ObjectiveTestItem { Name = "Center_Correlated_Color_Temperature", Unit = "K" };
        /// <summary>中心点亮度，单位 cd/m^2。</summary>
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem { Name = "Center_Lunimance ", Unit = "cd/m^2" };
        /// <summary>中心点 CIE 1931 色品坐标 x。</summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem { Name = "Center_CIE_1931Chromatic_Coordinates_x" };
        /// <summary>中心点 CIE 1931 色品坐标 y。</summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem { Name = "Center_CIE_1931Chromatic_Coordinates_y" };
        /// <summary>中心点 CIE 1976 色品坐标 u'。</summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem { Name = "Center_CIE_1976Chromatic_Coordinates_u'" };
        /// <summary>中心点 CIE 1976 色品坐标 v'。</summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem { Name = "Center_CIE_1976Chromatic_Coordinates_v'" };
    }
}
