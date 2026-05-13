using CVCommCore.CVImage;

namespace MQTTMessageLib.Camera;

public class CameraGetDataResult
{
	public string Filename { get; set; }

	public CameraFileType FileType { get; set; }

	public CVCIEFileInfo CIEFileInfo { get; set; }

	public bool IsLocal { get; set; }

	public string MapName { get; set; }

	public long TotalTime { get; set; }
}
