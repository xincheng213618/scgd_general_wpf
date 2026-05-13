namespace MQTTMessageLib.SMU;

public class MQTTSMUScanResultData
{
	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public MQTTSMUScanResultData(DeviceSMUScanResponse response)
	{
		MasterId = response.MasterId;
		MasterResultType = response.DeviceResultType;
	}

	public MQTTSMUScanResultData(SMUScanResultData result)
	{
	}
}
