namespace MQTTMessageLib.Calibration;

public class DeviceCalibrationDeleteData : DeviceCVBaseNoParamRequest<CalibrationRequestType>, IDevCalibrationRequest, IDeviceRequest
{
	public DeviceCalibrationDeleteData(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, CalibrationRequestType.DeleteData)
	{
	}
}
