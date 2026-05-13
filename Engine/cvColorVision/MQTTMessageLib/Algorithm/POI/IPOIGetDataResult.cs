using CVCommCore;

namespace MQTTMessageLib.Algorithm.POI;

public interface IPOIGetDataResult
{
	AlgorithmResultType ResultType { get; }

	int RecordCount { get; }
}
