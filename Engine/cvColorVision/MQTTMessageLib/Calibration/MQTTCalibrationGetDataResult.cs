namespace MQTTMessageLib.Calibration;

public class MQTTCalibrationGetDataResult : MQTTCalibrationResult
{
	public bool IsLocal { get; set; }

	public string MapName { get; set; }

	public MQTTCalibrationGetDataResult(string imgFileName, string templateName, int masterId, string mapName, bool isLocal)
		: base(imgFileName, templateName, masterId)
	{
		IsLocal = isLocal;
		MapName = mapName;
	}
}
