namespace MQTTMessageLib.PG;

public interface IDevPGRequest : IDeviceRequest
{
	PGRequestType DeviceRequestType { get; }
}
