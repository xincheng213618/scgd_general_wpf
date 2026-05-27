using ColorVision.Engine.PropertyEditor;
using ColorVision.Scheduler;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Services.Devices.Camera.Job
{
    /// <summary>
    /// Configuration for camera capture job
    /// </summary>
    public class CameraCaptureJobConfig : JobConfigBase
    {
        [Display(Name = "Engine_PG_CameraDeviceName", GroupName = "Engine_PG_CameraSettings", Description = "Engine_PG_CameraDeviceNameDesc", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCamera))]
        public string DeviceCameraName { get => _DeviceCameraName; set { _DeviceCameraName = value; OnPropertyChanged(); } }
        private string _DeviceCameraName;
    }
}

