namespace MQTTMessageLib.SMU;

public class SMUCloseOutputRequestParam
{
	public SMUChannelType Channel { get; set; }

	public bool IsSrcA => Channel == SMUChannelType.A;
}
