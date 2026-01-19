using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Scheduler;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Camera.Job
{
    /// <summary>
    /// Configuration for camera capture job
    /// </summary>
    public class CameraCaptureJobConfig : JobConfigBase
    {
        [Category("相机设置")]
        [DisplayName("相机设备名称")]
        [Description("输入要使用的相机设备名称")]
        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCamera))]
        public string DeviceCameraName { get => _DeviceCameraName; set { _DeviceCameraName = value; OnPropertyChanged(); } }
        private string _DeviceCameraName;
    }
}

