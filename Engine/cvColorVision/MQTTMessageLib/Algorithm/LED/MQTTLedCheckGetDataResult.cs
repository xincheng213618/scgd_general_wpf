using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.LED;

public class MQTTLedCheckGetDataResult : MQTTAlgorithmGetDataResult
{
	public List<LedCheckResult> Results { get; set; }

	public MQTTLedCheckGetDataResult(string imgFileName, string templateName, List<LedCheckResult> results, int masterId)
		: base(AlgorithmResultType.LedCheck, imgFileName, templateName, masterId)
	{
	}
}
