using Newtonsoft.Json;

namespace FlowEngineLib.Algorithm;

public class CalibrationData : AlgorithmParam
{
	// Only legacy calibration loops supply an exposure override.
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public CVTemplateParam ExpTemplateParam { get; set; }

	public bool IsSaveCIE { get; set; }

	public CalibrationData(string expTempName, AlgorithmPreStepParam param, bool isSaveCIE)
		: base(param, string.Empty)
	{
		if (!string.IsNullOrEmpty(expTempName)) ExpTemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = expTempName
		};
		IsSaveCIE = isSaveCIE;
	}
}
