namespace MQTTMessageLib.Calibration;

public interface IDevCalibrationRequest : IDeviceRequest
{
	CalibrationRequestType DeviceRequestType { get; }
}
