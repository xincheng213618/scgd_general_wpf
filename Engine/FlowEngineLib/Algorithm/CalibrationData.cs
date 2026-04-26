namespace FlowEngineLib.Algorithm;

public class CalibrationData : AlgorithmParam
{
	public int OrderIndex { get; set; }

	public CVTemplateParam ExpTemplateParam { get; set; }

	public POITemplateParam POIParam { get; set; }

	public CalibrationData(string expTempName, AlgorithmPreStepParam param, int orderIndex)
		: base(param, string.Empty)
	{
		ExpTemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = expTempName
		};
		OrderIndex = orderIndex;
	}
}
