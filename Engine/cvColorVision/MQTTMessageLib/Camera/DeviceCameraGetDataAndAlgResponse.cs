using MQTTMessageLib.Algorithm;
using MQTTMessageLib.Calibration;

namespace MQTTMessageLib.Camera;

public class DeviceCameraGetDataAndAlgResponse : DeviceCameraResultResponse<CameraGetDataResult>
{
	public DeviceCalibrationGetDataResponse CaliResponse { get; set; }

	public DeviceAlgorithmBaseResponse AlgResponse { get; set; }

	public int DeviceResultMasterId => GetResultMasterId();

	public int DeviceResultType { get; private set; }

	private int GetResultMasterId()
	{
		if (AlgResponse != null)
		{
			DeviceResultType = AlgResponse.DeviceResultType;
			return AlgResponse.MasterId;
		}
		if (CaliResponse != null)
		{
			DeviceResultType = 100;
			return CaliResponse.MasterId;
		}
		DeviceResultType = 100;
		return base.MasterId;
	}

	public DeviceCameraGetDataAndAlgResponse(CameraGetDataResult result, CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.GetDataAndAlg, result, status, totalTime)
	{
	}

	public DeviceCameraGetDataAndAlgResponse(DeviceCameraGetDataResponse response)
		: this(response.Result, response, response.TotalTime)
	{
		CaliResponse = response.CaliResponse;
	}
}
