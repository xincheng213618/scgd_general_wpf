using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.XR;

public struct MTFResult(POIPointOnly point, MTFResultData data)
{
	public POIPointOnly Point = point;

	public MTFResultData Data = data;
}
