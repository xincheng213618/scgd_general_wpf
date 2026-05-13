using System.Collections.Generic;

namespace MQTTMessageLib.Flow;

public class DeviceFlowRunParam<T>
{
	public string Name { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public List<T> Services { get; set; }
}
