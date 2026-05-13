namespace MQTTMessageLib.SMU;

public class SMUMeasureResultData : SMUMasterResultData
{
	public SMUChannelType ChannelType { get; set; }

	public bool IsSourceV { get; set; }

	public SMUMeasureResultData(SMUChannelType channelType, bool isSourceV, double v, double i)
	{
		ChannelType = channelType;
		IsSourceV = isSourceV;
		base.V = v;
		base.I = i;
	}
}
