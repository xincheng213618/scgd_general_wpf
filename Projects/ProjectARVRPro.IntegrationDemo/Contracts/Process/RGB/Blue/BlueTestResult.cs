using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.RGB.Blue
{
    /// <summary>
    /// 蓝场测试结果，包含蓝色画面的亮度均匀性、色度均匀性和中心光色参数。
    /// </summary>
    public class BlueTestResult : ViewModelBase
    {
        /// <summary>测点光色数据列表。</summary>
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();
        /// <summary>蓝场亮度均匀性，通常按最小亮度/最大亮度*100% 计算，单位 %。</summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem();
        /// <summary>蓝场色度均匀性，通常取最大 Delta u'v'，越小越均匀。</summary>
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem();
        /// <summary>蓝场中心点亮度，单位 cd/m^2。</summary>
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem();
        /// <summary>蓝场中心点 CIE 1931 色品坐标 x。</summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem();
        /// <summary>蓝场中心点 CIE 1931 色品坐标 y。</summary>
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem();
        /// <summary>蓝场中心点 CIE 1976 色品坐标 u'。</summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem();
        /// <summary>蓝场中心点 CIE 1976 色品坐标 v'。</summary>
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem();
    }
}
