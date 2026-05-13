namespace MQTTMessageLib.FileServer;

public interface IDevFileServerResponse : IDeviceResponse
{
	long TotalTime { get; set; }
}
