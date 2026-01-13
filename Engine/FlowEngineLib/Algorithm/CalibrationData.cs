namespace FlowEngineLib.Algorithm;

public class CalibrationData : AlgorithmParam
{
	public int OrderIndex { get; set; }

	public CVTemplateParam ExpTemplateParam { get; set; }

	public POITemplateParam POIParam { get; set; }

	public CalibrationData(string expTempName, AlgorithmPreStepParam param, string globalVariableName, int orderIndex)
		: base(param, globalVariableName)
	{
		ExpTemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = expTempName
		};
		OrderIndex = orderIndex;
	}
}
