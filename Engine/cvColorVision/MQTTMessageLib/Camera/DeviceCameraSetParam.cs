namespace MQTTMessageLib.Camera;

public class DeviceCameraSetParam : DeviceCVBaseRequest<CameraRequestType, SetParamParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraSetParam(string deviceCode, string serialNumber, SetParamParam param)
		: base(deviceCode, serialNumber, CameraRequestType.SetParam, param)
	{
	}
}
