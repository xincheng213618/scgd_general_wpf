using ColorVision.Engine.PropertyEditor;
using ColorVision.Scheduler;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace ColorVision.Engine.Services.Devices.Spectrum.Job
{
    /// <summary>
    /// Configuration for spectrum data acquisition job
    /// </summary>
    public class SpectrumGetDataJobConfig : JobConfigBase
    {
        [Display(Name = "Engine_PG_SpectrometerDeviceName", GroupName = "Engine_PG_SpectrometerSettings", Description = "Engine_PG_SpectrometerDeviceNameDesc", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(DeviceNameEditor)),DeviceSourceType(typeof(DeviceSpectrum))]
        public string DeviceSpectrumName { get => _DeviceSpectrumName; set { _DeviceSpectrumName = value; OnPropertyChanged(); } }
        private string _DeviceSpectrumName;
    }
}
