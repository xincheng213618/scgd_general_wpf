namespace MQTTMessageLib.Calibration;

public class DeviceParamCalibration
{
	public CVTemplateParam TemplateParam { get; set; }

	public string ImgFileName { get; set; }

	public float gain { get; set; }

	public float[] exp { get; set; }
}
