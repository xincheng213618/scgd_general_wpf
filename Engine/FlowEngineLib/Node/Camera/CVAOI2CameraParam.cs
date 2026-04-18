namespace FlowEngineLib.Node.Camera;

public class CVAOI2CameraParam : CVAOICameraParam
{
	public int POI_MasterId { get; set; }

	public CVAOI2CameraParam(string camTempName, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, string algParamType, string algTempName, int POIMasterId, int imgSaveBit)
		: base(camTempName, isWithND, isAutoExpTime, autoExpTempName, caliTempName, algParamType, algTempName, imgSaveBit)
	{
		POI_MasterId = POIMasterId;
	}
}
