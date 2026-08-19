using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTNodeServiceStatus
{
	public string ServiceCode { get; set; }

	public string LiveTime { get; set; }

	public List<MQTTDeviceStatus> DeviceList { get; set; }
}
