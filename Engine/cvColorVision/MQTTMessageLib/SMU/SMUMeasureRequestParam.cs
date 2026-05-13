namespace MQTTMessageLib.SMU;

public class SMUMeasureRequestParam
{
	public SMUChannelType Channel { get; set; }

	public bool IsSrcA => Channel == SMUChannelType.A;

	public bool IsSourceV { get; set; }

	public bool IsAutoRng { get; set; } = true;

	public double SrcRng { get; set; }

	public double LmtRng { get; set; }

	public double MeasureValue { get; set; }

	public double LimitValue { get; set; }

	public SMUMeasureRequestParam()
	{
	}

	public SMUMeasureRequestParam(DeviceParamScan scanParam)
	{
		IsSourceV = scanParam.IsSourceV;
		IsAutoRng = scanParam.IsAutoRng;
		SrcRng = scanParam.SrcRng;
		LmtRng = scanParam.LmtRng;
		Channel = scanParam.Channel;
		MeasureValue = scanParam.BeginValue;
		LimitValue = scanParam.LimitValue;
	}
}
