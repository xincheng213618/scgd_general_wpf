namespace MQTTMessageLib.Spectrum;

public interface IDevSpectrumRequest : IDeviceRequest
{
	SPRequestType DeviceRequestType { get; }
}
