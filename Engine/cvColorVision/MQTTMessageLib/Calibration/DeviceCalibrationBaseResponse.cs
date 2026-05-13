namespace MQTTMessageLib.Calibration;

public class DeviceCalibrationBaseResponse : CVBaseDeviceResponseWithResult, IDevCalibrationResponse, IDeviceResponseWithResult, IDeviceResponse
{
	public CalibrationResultType ResultType { get; set; }

	public DeviceCalibrationBaseResponse(CalibrationResultType resultType, int code, string desc, long totalTime)
		: base((int)resultType, code, desc, totalTime)
	{
		ResultType = resultType;
	}

	public DeviceCalibrationBaseResponse(CalibrationResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: base((int)resultType, status, totalTime)
	{
		ResultType = resultType;
	}

	public static IDevCalibrationResponse Success(CalibrationResultType resultType, long totalTime)
	{
		return new DeviceCalibrationBaseResponse(resultType, 0, "ok", totalTime);
	}
}
