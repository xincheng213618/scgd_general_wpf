using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.Algorithm;

namespace FlowEngineLib;

public class CVAOIBVRegCameraParam : CVAOIBVCameraParam
{
	public int POI_MasterId { get; set; }

	public CVTemplateParam AlgParamTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public CVOLED_Channel Channel { get; set; }

	public string AlgParamType { get; set; }

	public CVAOIBVRegCameraParam(string imgSaveName, CVImageFlipMode flipMode, int avgCount, float gain, float[] expTime, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, string algParamType, string algTempName, CVOLED_Channel channel, string outputTempName, int POIMasterId, int bitSaveRawImg)
		: base(imgSaveName, gain, avgCount, flipMode, expTime, isWithND, isAutoExpTime, autoExpTempName, caliTempName, bitSaveRawImg)
	{
		POI_MasterId = POIMasterId;
		Channel = channel;
		AlgParamType = algParamType;
		AlgParamTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = algTempName
		};
		OutputTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = outputTempName
		};
	}
}
