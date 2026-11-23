using ColorVision.Engine.PropertyEditor;
using ColorVision.Scheduler;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace ColorVision.Engine.Services.Devices.Spectrum.Job
{
    /// <summary>
    /// Configuration for spectrum data acquisition job
    /// </summary>
    public class SpectrumGetDataJobConfig : JobConfigBase
    {
        [Display(Name = nameof(Properties.Resources.Engine_PG_SpectrometerDeviceName), GroupName = nameof(Properties.Resources.Engine_PG_SpectrometerSettings), Description = nameof(Properties.Resources.Engine_PG_SpectrometerDeviceNameDesc), ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceSpectrum), LegacyReferenceProperty = nameof(DeviceSpectrumName))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? DeviceSpectrumCode { get => _deviceSpectrumCode; set { _deviceSpectrumCode = value; OnPropertyChanged(); } }
        private string? _deviceSpectrumCode;

        [Browsable(false), JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? DeviceSpectrumName { get; set; }
    }
}
