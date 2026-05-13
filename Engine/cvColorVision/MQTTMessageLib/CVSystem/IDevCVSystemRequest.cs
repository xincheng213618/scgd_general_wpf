namespace MQTTMessageLib.CVSystem;

public interface IDevCVSystemRequest : IDeviceRequest
{
	CVSystemRequestType DeviceRequestType { get; }
}
