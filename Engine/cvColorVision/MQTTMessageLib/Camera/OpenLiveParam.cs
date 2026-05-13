namespace MQTTMessageLib.Camera;

public class OpenLiveParam
{
	public string RemoteIP { get; set; }

	public int RemotePort { get; set; }

	public float ExpTime { get; set; }

	public bool IsLocal { get; set; }

	public int FlipCode { get; set; }
}
