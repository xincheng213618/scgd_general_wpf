namespace MQTTMessageLib.Camera;

public class DeviceCameraGetTemp : DeviceCVBaseRequest<CameraRequestType, GetTempParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraGetTemp(string deviceCode, string serialNumber, int beforeMinutes)
		: base(deviceCode, serialNumber, CameraRequestType.GetTemperature, new GetTempParam
		{
			BeforeMinutes = beforeMinutes
		})
	{
	}
}
