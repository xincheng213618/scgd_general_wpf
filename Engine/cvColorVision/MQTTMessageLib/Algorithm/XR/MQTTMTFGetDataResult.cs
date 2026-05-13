using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class MQTTMTFGetDataResult : MQTTAlgorithmGetDataResult
{
	public List<MTFResult> Results { get; set; }

	public MQTTMTFGetDataResult(string imgFileName, string templateName, List<MTFResult> results, int masterId)
		: base(AlgorithmResultType.MTF, imgFileName, templateName, masterId)
	{
	}
}
