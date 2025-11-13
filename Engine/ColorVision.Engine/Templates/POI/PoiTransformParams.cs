using System.ComponentModel;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// POI点变换参数，用于批量调整POI点的位置和尺寸
    /// </summary>
    [DisplayName("POI变换参数")]
    public class PoiTransformParams
    {
        /// <summary>
        /// X轴偏移量（减去该值）
        /// </summary>
        [Category("位置偏移")]
        [DisplayName("X偏移")]
        [Description("所有POI点的X坐标将减去此值")]
        public double OffsetX { get; set; } = 0;

        /// <summary>
        /// Y轴偏移量（减去该值）
        /// </summary>
        [Category("位置偏移")]
        [DisplayName("Y偏移")]
        [Description("所有POI点的Y坐标将减去此值")]
        public double OffsetY { get; set; } = 0;

        /// <summary>
        /// X轴缩放比例
        /// </summary>
        [Category("缩放")]
        [DisplayName("X缩放")]
        [Description("X坐标缩放倍数，1.0表示不缩放")]
        public double ScaleX { get; set; } = 1.0;

        /// <summary>
        /// Y轴缩放比例
        /// </summary>
        [Category("缩放")]
        [DisplayName("Y缩放")]
        [Description("Y坐标缩放倍数，1.0表示不缩放")]
        public double ScaleY { get; set; } = 1.0;

        /// <summary>
        /// 是否同时缩放POI点的宽度和高度
        /// </summary>
        [Category("缩放")]
        [DisplayName("缩放尺寸")]
        [Description("是否同时缩放POI点的宽度和高度")]
        public bool ScaleSize { get; set; } = true;
    }
}
