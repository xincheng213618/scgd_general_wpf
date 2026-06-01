using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public enum GenType
    {
        /// <summary>
        /// 差值
        /// </summary>
        [Display(Name = "Engine_PG_Difference", ResourceType = typeof(Properties.Resources))]
        Difference,
        /// <summary>
        /// 比例
        /// </summary>
        [Display(Name = "Engine_PG_Ratio", ResourceType = typeof(Properties.Resources))]
        Ratio
    }
}
