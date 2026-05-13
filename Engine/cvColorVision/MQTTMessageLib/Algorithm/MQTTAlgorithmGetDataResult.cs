using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class MQTTAlgorithmGetDataResult : MQTTAlgorithmBaseResult
{
	public string ImgFileName { get; set; }

	public string TemplateName { get; set; }

	public MQTTAlgorithmGetDataResult(AlgorithmResultType resultType, string imgFileName, string templateName, int masterId, string masterResultCode = null)
		: base(masterId, resultType, masterResultCode)
	{
		TemplateName = templateName;
		ImgFileName = imgFileName;
	}
}
