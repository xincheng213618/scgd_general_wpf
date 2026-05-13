namespace MQTTMessageLib.SMU;

public class SMUScanRequestParam
{
	public bool IsCloseOutput { get; set; }

	public bool IsSrcA => DeviceParam.Channel == SMUChannelType.A;

	public DeviceParamScan DeviceParam { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public SMUScanRequestParam()
	{
		DeviceParam = new DeviceParamScan();
	}
}
