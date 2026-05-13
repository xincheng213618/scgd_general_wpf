using CVCommCore.CVImage;

namespace MQTTMessageLib.Algorithm;

public struct ROISFRResult
{
	public ROISFRResult_OtherInfo OtherInfo { get; set; }

	public CRECT rtROI { get; set; }

	public HImage tImg { get; set; }
}
