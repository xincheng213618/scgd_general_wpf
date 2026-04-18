namespace FlowEngineLib.Node.Camera;

public class CVAOICameraParam : CommCameraData
{
	public string AlgParamType { get; set; }

	public CVTemplateParam AlgParamTemplate { get; set; }

	public int ImageSaveBpp { get; set; }

	public CVAOICameraParam(string camTempName, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, string algParamType, string algTempName, int imgSaveBit)
		: base(camTempName, isWithND, isAutoExpTime, autoExpTempName, caliTempName, string.Empty, string.Empty, string.Empty, string.Empty)
	{
		AlgParamType = algParamType;
		AlgParamTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = algTempName
		};
		ImageSaveBpp = imgSaveBit;
	}
}
