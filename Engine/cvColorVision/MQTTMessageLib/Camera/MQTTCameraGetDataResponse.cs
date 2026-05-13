namespace MQTTMessageLib.Camera;

public class MQTTCameraGetDataResponse : MQTTCVBaseResponse<MQTTCameraGetDataResult>
{
	public MQTTCameraGetDataResponse(MQTTCVRequestHeader request, DeviceCameraGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraGetDataResult(response.ResultMasterId, response.POIResponse, response.ResultTotalTime))
	{
	}

	public MQTTCameraGetDataResponse(MQTTCVRequestHeader request, DeviceCameraBaseResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraGetDataResult(response.MasterId, null, response.TotalTime))
	{
	}

	public MQTTCameraGetDataResponse(MQTTCVRequestHeader request, CVBaseDeviceResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), (MQTTCameraGetDataResult)null)
	{
	}
}
