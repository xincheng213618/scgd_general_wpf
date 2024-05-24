using System.ComponentModel;

namespace ColorVision.Services.Devices.SMU.Views
{
    public enum MeasurementType
    {
        [Description("电压")]
        Voltage,
        [Description("电流")]
        Current
    }
}
