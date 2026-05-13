using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class MQTTSFRGetDataResult : MQTTAlgorithmGetDataResult
{
	public List<SFRResult> Results { get; set; }

	public MQTTSFRGetDataResult(string imgFileName, string templateName, List<SFRResult> results, int masterId)
		: base(AlgorithmResultType.SFR, imgFileName, templateName, masterId)
	{
	}
}
