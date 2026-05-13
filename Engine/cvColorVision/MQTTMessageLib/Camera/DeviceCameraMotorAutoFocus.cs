namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorAutoFocus : DeviceCVBaseRequest<CameraRequestType, AutoFocusRunParam>, IDevCameraRequest, IDeviceRequest
{
	public string ImageFileName { get; set; }

	public int ImageNumber { get; set; }

	public DeviceCameraMotorAutoFocus(string deviceCode, string serialNumber, AutoFocusRunParam param)
		: base(deviceCode, serialNumber, CameraRequestType.Motor_AutoFocus, param)
	{
		ImageFileName = string.Empty;
		ImageNumber = 2;
	}
}
