using CVCommCore.CVImage;
using MQTTMessageLib.Camera;

namespace MQTTMessageLib.Calibration;

public class CalibrationGetDataResult
{
	public string Filename { get; set; }

	public CameraFileType FileType { get; set; }

	public CVCIEFileInfo ImageInfo { get; set; }

	public bool IsLocal { get; set; }

	public string MapName { get; set; }
}
