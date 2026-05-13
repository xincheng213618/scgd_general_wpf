namespace MQTTMessageLib.Calibration;

public class MQTTCalibrationGetData : MQTTCVBaseRequest<CalibrationGetDataParam>
{
	public MQTTCalibrationGetData(string serviceName, string deviceName, CalibrationGetDataParam data)
		: base(serviceName, deviceName, "Calibration", data)
	{
	}
}
