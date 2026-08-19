using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTNodeService
{
	public string ServiceToken { get; set; }

	public string ServiceCode { get; set; }

	public string ServiceType { get; set; }

	public string UpChannel { get; set; }

	public string DownChannel { get; set; }

	public Dictionary<string, MQTTNodeServiceDevice> Devices { get; set; } = new();
}
