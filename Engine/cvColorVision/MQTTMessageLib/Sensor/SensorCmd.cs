namespace MQTTMessageLib.Sensor;

public class SensorCmd
{
	public string Name { get; set; }

	public SensorCmdType CmdType { get; set; }

	public string Request { get; set; }

	public string Response { get; set; }

	public int RetryCount { get; set; } = 3;

	public int Delay { get; set; } = 1000;

	public int Timeout { get; set; } = 5000;
}
