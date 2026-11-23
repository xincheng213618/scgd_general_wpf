using ColorVision.Engine.PropertyEditor;
using ColorVision.Scheduler;
using Newtonsoft.Json;
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
        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCamera), LegacyReferenceProperty = nameof(DeviceCameraName))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? DeviceCameraCode { get => _deviceCameraCode; set { _deviceCameraCode = value; OnPropertyChanged(); } }
        private string? _deviceCameraCode;

        [Browsable(false), JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? DeviceCameraName { get; set; }
    }
}

