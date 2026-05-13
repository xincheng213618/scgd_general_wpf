using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class MQTTAlgorithmBaseResult
{
	public int MasterId { get; private set; }

	public AlgorithmResultType ResultType { get; private set; }

	public string MasterResultCode { get; private set; }

	public MQTTAlgorithmBaseResult(int masterId, AlgorithmResultType resultType, string masterResultCode)
	{
		MasterId = masterId;
		ResultType = resultType;
		MasterResultCode = masterResultCode;
	}
}
