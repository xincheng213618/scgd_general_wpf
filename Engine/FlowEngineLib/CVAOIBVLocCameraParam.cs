using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.Algorithm;

namespace FlowEngineLib;

public class CVAOIBVLocCameraParam : CVAOIBVCameraParam
{
	public CVTemplateParam AlgParamTemplate { get; set; }

	public CVOLED_Channel Channel { get; set; }

	public string AlgParamType { get; set; }

	public CVAOIBVLocCameraParam(string imgSaveName, CVImageFlipMode flipMode, int avgCount, float gain, float[] expTime, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, string algParamType, string algTempName, CVOLED_Channel channel, int bitSaveRawImg)
		: base(imgSaveName, gain, avgCount, flipMode, expTime, isWithND, isAutoExpTime, autoExpTempName, caliTempName, bitSaveRawImg)
	{
		Channel = channel;
		AlgParamType = algParamType;
		AlgParamTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = algTempName
		};
	}
}
