using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class MQTTFOVGetDataResult : MQTTAlgorithmGetDataResult
{
	public List<FOVResult> Results { get; set; }

	public MQTTFOVGetDataResult(string imgFileName, string templateName, List<FOVResult> results, int masterId)
		: base(AlgorithmResultType.FOV, imgFileName, templateName, masterId)
	{
	}
}
