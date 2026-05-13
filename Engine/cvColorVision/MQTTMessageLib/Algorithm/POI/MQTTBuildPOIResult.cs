using System.Collections.Generic;
using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class MQTTBuildPOIResult : MQTTAlgorithmGetDataResult
{
	public List<POIPointOnly> Results { get; set; }

	public MQTTBuildPOIResult(string imgFileName, string templateName, List<POIPointOnly> results, int masterId)
		: base(AlgorithmResultType.BuildPOI, imgFileName, templateName, masterId)
	{
	}
}
