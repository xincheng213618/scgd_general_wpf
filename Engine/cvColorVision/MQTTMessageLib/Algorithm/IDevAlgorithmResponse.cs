using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public interface IDevAlgorithmResponse : IDeviceResponseWithResult, IDeviceResponse
{
	AlgorithmResultType ResultType { get; set; }

	string OutImgFileName { get; set; }
}
