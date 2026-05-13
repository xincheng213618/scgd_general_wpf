using CVCommCore.CVImage;

namespace MQTTMessageLib.Camera;

public struct DeviceOpenLiveResult
{
	public bool IsLocal { get; set; }

	public string MapName { get; set; }

	public SrcFrameInfo FrameInfo { get; set; }
}
