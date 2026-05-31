namespace ProjectARVRPro.Process
{
    /// <summary>
    /// 单个 POI 测点的光色数据。
    /// </summary>
    public class PoixyuvData
    {
        /// <summary>测点序号。</summary>
        public int Id { get; set; }
        /// <summary>测点名称。</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>相关色温，单位 K。</summary>
        public double CCT { get; set; }
        /// <summary>主波长或波长相关结果。</summary>
        public double Wave { get; set; }
        /// <summary>CIE XYZ 三刺激值 X。</summary>
        public double X { get; set; }
        /// <summary>CIE XYZ 三刺激值 Y，通常也可理解为亮度相关值。</summary>
        public double Y { get; set; }
        /// <summary>CIE XYZ 三刺激值 Z。</summary>
        public double Z { get; set; }
        /// <summary>CIE 1976 色品坐标 u'。</summary>
        public double u { get; set; }
        /// <summary>CIE 1976 色品坐标 v'。</summary>
        public double v { get; set; }
        /// <summary>CIE 1931 色品坐标 x。</summary>
        public double x { get; set; }
        /// <summary>CIE 1931 色品坐标 y。</summary>
        public double y { get; set; }
    }
}
