using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

public class POIDataParam : AlgorithmParam
{
	public CVTemplateParam FilterTemplate { get; set; }

	public CVTemplateParam ReviseTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public bool IsSubPixel { get; set; }

	public bool IsCCTWave { get; set; }

	public POIDataParam(string imgFile, int tempId, string tempName, string filterTempName, string reviseTempName, string outTempName, AlgorithmPreStepParam param, bool isSubPixel, bool isCCTWave)
		: base(imgFile, tempId, tempName, param, string.Empty)
	{
		FilterTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = filterTempName
		};
		ReviseTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = reviseTempName
		};
		OutputTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = outTempName
		};
		IsSubPixel = isSubPixel;
		IsCCTWave = isCCTWave;
	}
}
