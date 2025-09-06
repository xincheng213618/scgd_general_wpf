namespace FlowEngineLib.Algorithm;

public class CalibrationData : AlgorithmParam
{
	public int OrderIndex { get; set; }

	public CVTemplateParam ExpTemplateParam { get; set; }

	public CalibrationData(string imgFile, int tempId, string tempName, string expTempName, AlgorithmPreStepParam param, string globalVariableName, int orderIndex)
		: base(imgFile, tempId, tempName, param, globalVariableName)
	{
		ExpTemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = expTempName
		};
		OrderIndex = orderIndex;
	}
}
