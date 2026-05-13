namespace MQTTMessageLib.FileServer;

public interface IDevFileServerRequest : IDeviceRequest
{
	FileServerRequestType DeviceRequestType { get; }
}
