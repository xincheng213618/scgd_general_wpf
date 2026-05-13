using MQTTMessageLib.FileServer;

namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumCalibrationUploadFileResponse : DeviceSpectrumResponse
{
	public DeviceFileUpdownParam Result { get; set; }

	public DeviceSpectrumCalibrationUploadFileResponse(CVBaseDeviceResponse status, long totalTime)
		: base(status, totalTime)
	{
	}

	public DeviceSpectrumCalibrationUploadFileResponse(int code, string desc, long totalTime)
		: base(code, desc, totalTime)
	{
	}

	public DeviceSpectrumCalibrationUploadFileResponse(DeviceFileUpdownParam result, int code, string desc, long totalTime)
		: this(code, desc, totalTime)
	{
		Result = result;
	}

	public static DeviceSpectrumCalibrationUploadFileResponse Success(DeviceFileUpdownParam result, long totalTime)
	{
		return new DeviceSpectrumCalibrationUploadFileResponse(result, 0, "ok", totalTime);
	}

	public static DeviceSpectrumCalibrationUploadFileResponse Failed(string desc, long totalTime)
	{
		return new DeviceSpectrumCalibrationUploadFileResponse(-1, desc, totalTime);
	}

	public static DeviceSpectrumCalibrationUploadFileResponse Pending(DeviceFileUpdownParam result, long totalTime)
	{
		return new DeviceSpectrumCalibrationUploadFileResponse(result, 102, "Pending", totalTime);
	}
}
