namespace MQTTMessageLib.Calibration;

public class MQTTCalibrationResult
{
	public string ImgFileName { get; set; }

	public string TemplateName { get; set; }

	public int MasterId { get; set; }

	public int ResultType { get; set; } = 30;

	public MQTTCalibrationResult(string imgFileName, string templateName, int masterId)
	{
		TemplateName = templateName;
		ImgFileName = imgFileName;
		MasterId = masterId;
	}
}
