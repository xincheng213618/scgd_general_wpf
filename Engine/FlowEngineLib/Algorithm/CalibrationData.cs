namespace FlowEngineLib.Algorithm;

public class CalibrationData : AlgorithmParam
{
	public int POI_MasterId { get; set; }

	public CVTemplateParam ExpTemplateParam { get; set; }

	public POITemplateParam POIParam { get; set; }

	public bool IsSaveCIE { get; set; }

	public CalibrationData(string expTempName, AlgorithmPreStepParam param, bool isSaveCIE)
		: base(param, string.Empty)
	{
		ExpTemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = expTempName
		};
		POI_MasterId = -1;
		IsSaveCIE = isSaveCIE;
	}
}
