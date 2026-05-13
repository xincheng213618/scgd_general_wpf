namespace MQTTMessageLib.Camera;

public class MQTTCameraGetDataAndAlgResponse : MQTTCVBaseResponse<MQTTCameraGetDataAndAlgResult>
{
	public MQTTCameraGetDataAndAlgResponse(MQTTCVRequestHeader request, DeviceCameraGetDataAndAlgResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraGetDataAndAlgResult(response.DeviceResultMasterId, response.DeviceResultType, response.TotalTime))
	{
	}

	public MQTTCameraGetDataAndAlgResponse(MQTTCVRequestHeader request, DeviceCameraBaseResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraGetDataAndAlgResult(response.MasterId, 100, response.TotalTime))
	{
	}

	public MQTTCameraGetDataAndAlgResponse(MQTTCVRequestHeader request, CVBaseDeviceResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), (MQTTCameraGetDataAndAlgResult)null)
	{
	}
}
