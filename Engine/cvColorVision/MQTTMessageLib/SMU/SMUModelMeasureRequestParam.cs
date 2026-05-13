namespace MQTTMessageLib.SMU;

public class SMUModelMeasureRequestParam
{
	public bool IsSrcA => DeviceParam.Channel == SMUChannelType.A;

	public DeviceParamScan DeviceParam { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public SMUModelMeasureRequestParam()
	{
		DeviceParam = new DeviceParamScan();
	}
}
