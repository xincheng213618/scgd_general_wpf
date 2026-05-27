using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public enum MeasurementType
    {
        [Display(Name = "Engine_PG_Voltage", ResourceType = typeof(Properties.Resources))]
        Voltage,
        [Display(Name = "Engine_PG_Current", ResourceType = typeof(Properties.Resources))]
        Current
    }
}
