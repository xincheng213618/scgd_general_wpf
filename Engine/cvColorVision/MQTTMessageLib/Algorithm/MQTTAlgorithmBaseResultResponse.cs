namespace MQTTMessageLib.Algorithm;

public class MQTTAlgorithmBaseResultResponse : MQTTCVBaseResponse<MQTTAlgorithmBaseResult>
{
	public MQTTAlgorithmBaseResultResponse(MQTTCVRequestHeader request, IDevAlgorithmResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTAlgorithmBaseResult(response.MasterId, response.ResultType, response.DeviceResultCode))
	{
	}

	public MQTTAlgorithmBaseResultResponse(MQTTCVRequestHeader request, IDeviceResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), (MQTTAlgorithmBaseResult)null)
	{
	}
}
