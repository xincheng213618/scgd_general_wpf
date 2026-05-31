using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.W25
{
    /// <summary>
    /// W25 白场 25 阶光色测试结果。
    /// </summary>
    public class W25TestResult : ViewModelBase
    {
        /// <summary>测点光色数据列表，包含亮度、色品坐标、色温等原始测点结果。</summary>
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();
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
