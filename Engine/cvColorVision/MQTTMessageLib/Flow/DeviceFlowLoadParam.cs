using System.Collections.Generic;

namespace MQTTMessageLib.Flow;

public class DeviceFlowLoadParam<T>
{
	public CVTemplateParam TemplateParam { get; set; }

	public List<T> Services { get; set; }
}
