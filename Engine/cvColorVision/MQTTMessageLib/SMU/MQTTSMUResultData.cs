namespace MQTTMessageLib.SMU;

public class MQTTSMUResultData
{
	public double V { get; set; }

	public double I { get; set; }

	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public MQTTSMUResultData(DeviceSMUMeasureResponse response)
	{
		MasterId = response.MasterId;
		MasterResultType = response.DeviceResultType;
		V = response.Data.V;
		I = response.Data.I;
	}
}
