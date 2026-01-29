using ColorVision.Engine.PropertyEditor;
using ColorVision.Scheduler;
using System.ComponentModel;


namespace ColorVision.Engine.Services.Devices.Spectrum.Job
{
    /// <summary>
    /// Configuration for spectrum data acquisition job
    /// </summary>
    public class SpectrumGetDataJobConfig : JobConfigBase
    {
        [Category("光谱仪设置")]
        [DisplayName("光谱仪设备名称")]
        [Description("输入要使用的光谱仪设备名称")]
        [PropertyEditorType(typeof(DeviceNameEditor)),DeviceSourceType(typeof(DeviceSpectrum))]
        public string DeviceSpectrumName { get => _DeviceSpectrumName; set { _DeviceSpectrumName = value; OnPropertyChanged(); } }
        private string _DeviceSpectrumName;
    }
}
