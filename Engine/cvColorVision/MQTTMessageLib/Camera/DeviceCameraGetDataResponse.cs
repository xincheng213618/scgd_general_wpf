using MQTTMessageLib.Algorithm;
using MQTTMessageLib.Calibration;

namespace MQTTMessageLib.Camera;

public class DeviceCameraGetDataResponse : DeviceCameraResultResponse<CameraGetDataResult>
{
	public DeviceAlgorithmResponse POIResponse { get; set; }

	public DeviceCalibrationGetDataResponse CaliResponse { get; set; }

	public long ResultTotalTime => GetTotalTime();

	public int ResultMasterId => GetMasterId();

	public DeviceCameraGetDataResponse(CameraGetDataResult result, CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.GetData, result, status, totalTime)
	{
	}

	public DeviceCameraGetDataResponse(CameraGetDataResult result, int code, string desc, long totalTime)
		: base(CameraResultType.GetData, result, code, desc, totalTime)
	{
	}

	private int GetMasterId()
	{
		if (CaliResponse != null && CaliResponse.MasterId > 0)
		{
			return CaliResponse.MasterId;
		}
		return base.MasterId;
	}

	private long GetTotalTime()
	{
		if (CaliResponse != null && CaliResponse.MasterId > 0)
		{
			return CaliResponse.TotalTime;
		}
		return base.TotalTime;
	}

	public static DeviceCameraGetDataResponse Success(CameraGetDataResult result, long totalTime)
	{
		return new DeviceCameraGetDataResponse(result, 0, "ok", totalTime);
	}

	public static DeviceCameraGetDataResponse Failed(string desc, long totalTime)
	{
		return new DeviceCameraGetDataResponse(null, -1, desc, totalTime);
	}
}
