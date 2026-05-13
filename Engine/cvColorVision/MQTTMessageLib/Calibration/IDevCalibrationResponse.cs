namespace MQTTMessageLib.Calibration;

public interface IDevCalibrationResponse : IDeviceResponseWithResult, IDeviceResponse
{
	CalibrationResultType ResultType { get; }
}
