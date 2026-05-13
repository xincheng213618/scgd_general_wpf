namespace MQTTMessageLib.SMU;

public class MQTTParamScanModel
{
	public SMUChannelType Channel { get; set; }

	public bool IsSourceV { get; set; }

	public double BeginValue { get; set; }

	public double EndValue { get; set; }

	public double LimitValue { get; set; }

	public int Points { get; set; }

	public MQTTParamScanModel(DeviceParamScan param)
	{
		Channel = param.Channel;
		BeginValue = param.BeginValue;
		EndValue = param.EndValue;
		IsSourceV = param.IsSourceV;
		LimitValue = param.LimitValue;
		Points = param.Points;
	}
}
