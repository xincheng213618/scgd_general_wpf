using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class MQTTGhostGetDataResult : MQTTAlgorithmGetDataResult
{
	public GhostResult Results { get; set; }

	public MQTTGhostGetDataResult(string imgFileName, string templateName, GhostResult results, int masterId)
		: base(AlgorithmResultType.Ghost, imgFileName, templateName, masterId)
	{
	}
}
