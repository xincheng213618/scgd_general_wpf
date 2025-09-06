namespace FlowEngineLib.Node.Camera;

public class CVAOICameraParam : CommCameraData
{
	public string AlgParamType { get; set; }

	public CVTemplateParam AlgParamTemplate { get; set; }

	public bool IsSaveRawImg { get; set; }

	public CVAOICameraParam(string camTempName, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, string algParamType, string algTempName, bool isHDR, bool isSaveRawImg)
		: base(camTempName, isWithND, isAutoExpTime, autoExpTempName, caliTempName, string.Empty, string.Empty, string.Empty, string.Empty, isHDR)
	{
		AlgParamType = algParamType;
		AlgParamTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = algTempName
		};
		IsSaveRawImg = isSaveRawImg;
	}
}
