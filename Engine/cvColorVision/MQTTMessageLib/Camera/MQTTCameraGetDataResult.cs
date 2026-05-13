using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.Camera;

public class MQTTCameraGetDataResult : MQTTCameraResult
{
	public CameraPOIResult POIResult { get; set; }

	public MQTTCameraGetDataResult(int masterId, DeviceAlgorithmResponse poi_response, long totalTime)
		: base(masterId, 100, totalTime)
	{
		if (poi_response != null)
		{
			POIResult = new CameraPOIResult
			{
				MasterId = poi_response.MasterId,
				MasterResultType = poi_response.DeviceResultType
			};
			base.MasterId = poi_response.MasterId;
			base.MasterResultType = poi_response.DeviceResultType;
		}
	}
}
