using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class MQTTDistortionGetDataResult : MQTTAlgorithmGetDataResult
{
	public List<DistortionResult> Results { get; set; }

	public MQTTDistortionGetDataResult(string imgFileName, string templateName, List<DistortionResult> results, int masterId)
		: base(AlgorithmResultType.Distortion, imgFileName, templateName, masterId)
	{
	}
}
