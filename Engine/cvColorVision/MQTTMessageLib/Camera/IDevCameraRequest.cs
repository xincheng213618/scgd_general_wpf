namespace MQTTMessageLib.Camera;

public interface IDevCameraRequest : IDeviceRequest
{
	CameraRequestType DeviceRequestType { get; }
}
