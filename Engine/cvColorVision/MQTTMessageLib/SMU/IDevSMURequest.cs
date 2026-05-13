namespace MQTTMessageLib.SMU;

public interface IDevSMURequest : IDeviceRequest
{
	SMURequestType DeviceRequestType { get; }
}
