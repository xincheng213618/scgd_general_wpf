using System.Collections.Generic;

namespace MQTTMessageLib.Camera;

public struct DeviceGetCameraIDs
{
	public List<DeviceCameraID> CameraIDs { get; set; }
}
