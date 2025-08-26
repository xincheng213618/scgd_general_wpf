using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class DataConvertData : AlgorithmPreStepParam
{
	public CVDataConvertInputType InType { get; set; }

	public CVDataConvertOutputType OutType { get; set; }

	public CVDataConvertMethodType MethodType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public DataConvertData(CVDataConvertMethodType methodType, string tempName, CVDataConvertInputType inType, CVDataConvertOutputType outType)
	{
		MethodType = methodType;
		InType = inType;
		OutType = outType;
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
