namespace MQTTMessageLib.Calibration;

public class MQTTCalibrationGetDataResultResponse : MQTTCVBaseResponse<MQTTCalibrationGetDataResult>
{
	public MQTTCalibrationGetDataResultResponse(MQTTCVRequestHeader request, DeviceCalibrationGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCalibrationGetDataResult(response.ImgFileName, response.TemplateName, response.MasterId, response.Result.MapName, response.Result.IsLocal))
	{
	}
}
