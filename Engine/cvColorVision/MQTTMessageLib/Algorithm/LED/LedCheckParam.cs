namespace MQTTMessageLib.Algorithm.LED;

public class LedCheckParam : DeviceAlgorithmParam
{
	public DeviceParamLedCheck DeviceParam { get; set; }

	public LedCheckParam()
	{
		DeviceParam = new DeviceParamLedCheck();
	}
}
