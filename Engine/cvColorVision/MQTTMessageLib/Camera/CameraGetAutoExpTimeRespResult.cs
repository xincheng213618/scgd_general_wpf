using System.Collections.Generic;

namespace MQTTMessageLib.Camera;

public struct CameraGetAutoExpTimeRespResult
{
	public List<CameraGetAutoExpTimeResult> ExpTime { get; set; }

	public int NDPort { get; set; }
}
