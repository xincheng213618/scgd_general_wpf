namespace FlowEngineLib;

public class LightingSensorData
{
	public string Channel { get; set; }

	public LightingSensorData(LightingChannel channel)
	{
		Channel = channel.ToString();
	}
}
