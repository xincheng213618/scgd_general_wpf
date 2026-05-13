using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.Calibration;

public class DeviceCalibrationGetDataResponse : DeviceCalibrationResponse
{
	public DeviceAlgorithmResponse POIResponse { get; set; }

	public CalibrationGetDataResult Result { get; set; }

	public DeviceCalibrationGetDataResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CalibrationResultType.Calibration, status, totalTime)
	{
	}

	public DeviceCalibrationGetDataResponse(CalibrationGetDataResult result, string imgFileName, string templateName, int code, string desc, long totalTime)
		: base(CalibrationResultType.Calibration, imgFileName, templateName, code, desc, totalTime)
	{
		Result = result;
	}

	public static DeviceCalibrationGetDataResponse Success(string imgFileName, string templateName, CalibrationGetDataResult result, long totalTime)
	{
		return new DeviceCalibrationGetDataResponse(result, imgFileName, templateName, 0, "ok", totalTime);
	}
}
