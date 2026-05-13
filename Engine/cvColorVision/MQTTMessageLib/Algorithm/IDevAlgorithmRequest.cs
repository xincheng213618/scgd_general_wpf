namespace MQTTMessageLib.Algorithm;

public interface IDevAlgorithmRequest : IDeviceRequest
{
	AlgorithmRequestType DeviceRequestType { get; }

	IDevAlgorithmRequest NextRequest { get; }
}
