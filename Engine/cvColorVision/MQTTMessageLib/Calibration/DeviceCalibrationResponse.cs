namespace MQTTMessageLib.Calibration;

public class DeviceCalibrationResponse : DeviceCalibrationBaseResponse
{
	public string ImgFileName { get; set; }

	public string TemplateName { get; set; }

	public DeviceCalibrationResponse(CalibrationResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, status, totalTime)
	{
	}

	public DeviceCalibrationResponse(CalibrationResultType resultType, string imgFileName, string templateName, int code, string desc, long totalTime)
		: base(resultType, code, desc, totalTime)
	{
		ImgFileName = imgFileName;
		TemplateName = templateName;
	}

	public static IDevCalibrationResponse Success(CalibrationResultType resultType, string imgFileName, string templateName, long totalTime)
	{
		return new DeviceCalibrationResponse(resultType, imgFileName, templateName, 0, "ok", totalTime);
	}

	public static IDevCalibrationResponse Failed(CalibrationResultType resultType, string imgFileName, string templateName, string errorDesc, long totalTime)
	{
		return new DeviceCalibrationResponse(resultType, imgFileName, templateName, -1, errorDesc, totalTime);
	}
}
