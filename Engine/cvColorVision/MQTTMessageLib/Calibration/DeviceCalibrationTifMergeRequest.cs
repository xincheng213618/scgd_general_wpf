namespace MQTTMessageLib.Calibration;

public class DeviceCalibrationTifMergeRequest : DeviceCVBaseRequest<CalibrationRequestType, CalibrationGetDataParam>, IDevCalibrationRequest, IDeviceRequest
{
	public DeviceCalibrationTifMergeRequest(string deviceName, string serialNumber, int zindex, CalibrationGetDataParam param)
		: base(deviceName, serialNumber, zindex, CalibrationRequestType.Tif_Merge, param)
	{
	}
}
