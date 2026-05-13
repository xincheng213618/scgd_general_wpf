namespace MQTTMessageLib.Calibration;

public class MQTTCalibrationResultResponse : MQTTCVBaseResponse<MQTTCalibrationResult>
{
	public MQTTCalibrationResultResponse(MQTTCVRequestHeader request, DeviceCalibrationResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCalibrationResult(response.ImgFileName, response.TemplateName, response.MasterId))
	{
	}

	public MQTTCalibrationResultResponse(MQTTCVRequestHeader request, IDeviceResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), (MQTTCalibrationResult)null)
	{
	}
}
