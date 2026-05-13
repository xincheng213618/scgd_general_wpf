using System.Collections.Generic;

namespace MQTTMessageLib.Flow;

public class DeviceFlowCombinedRunParam<T>
{
	public string Name { get; set; }

	public int Timeout { get; set; } = 600;

	public CVTemplateParam TemplateParam { get; set; }

	public List<T> Services { get; set; }
}
