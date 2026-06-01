using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// POI点变换参数，用于批量调整POI点的位置和尺寸
    /// </summary>
    [Display(Name = "Engine_PG_PoiTransformParams", ResourceType = typeof(Properties.Resources))]
    public class PoiTransformParams
    {
        /// <summary>
        /// X轴偏移量（减去该值）
        /// </summary>
        [Display(Name = "Engine_PG_XOffset", GroupName = "Engine_PG_PositionOffset", Description = "Engine_PG_XOffsetDesc", ResourceType = typeof(Properties.Resources))]
        public double OffsetX { get; set; } = 0;

        /// <summary>
        /// Y轴偏移量（减去该值）
        /// </summary>
        [Display(Name = "Engine_PG_YOffset", GroupName = "Engine_PG_PositionOffset", Description = "Engine_PG_YOffsetDesc", ResourceType = typeof(Properties.Resources))]
        public double OffsetY { get; set; } = 0;

        /// <summary>
        /// X轴缩放比例
        /// </summary>
        [Display(Name = "Engine_PG_XScale", GroupName = "Engine_PG_Scaling", Description = "Engine_PG_XScaleDesc", ResourceType = typeof(Properties.Resources))]
        public double ScaleX { get; set; } = 1.0;

        /// <summary>
        /// Y轴缩放比例
        /// </summary>
        [Display(Name = "Engine_PG_YScale", GroupName = "Engine_PG_Scaling", Description = "Engine_PG_YScaleDesc", ResourceType = typeof(Properties.Resources))]
        public double ScaleY { get; set; } = 1.0;

        /// <summary>
        /// 是否同时缩放POI点的宽度和高度
        /// </summary>
        [Display(Name = "Engine_PG_ScaleSize", GroupName = "Engine_PG_Scaling", Description = "Engine_PG_ScaleSizeDesc", ResourceType = typeof(Properties.Resources))]
        public bool ScaleSize { get; set; } = true;
    }
}
