using CVCommCore.CVImage;
using Newtonsoft.Json;

namespace MQTTMessageLib.Camera;

public class CameraGetDataParam
{
	public CVImageFlipMode FlipMode { get; set; } = CVImageFlipMode.None;

	public POITemplateParam POIParam { get; set; }

	public CVTemplateParam Calibration { get; set; }

	public CVTemplateParam CamParamTemplate { get; set; }

	public CVTemplateParam AutoExpTimeTemplate { get; set; }

	public bool IsAutoExpTime { get; set; }

	public bool IsAutoExpWithND { get; set; }

	public uint AvgCount { get; set; }

	public float Gain { get; set; }

	public int NDPort { get; set; }

	public int ImageSaveBpp { get; set; } = 16;

	public string ImageOutName { get; set; }

	[JsonIgnore]
	public CameraRunParam DeviceParam { get; set; }

	[JsonIgnore]
	public DeviceCameraHDRParam DeviceCameraHDRParam { get; set; }

	public float[] ExpTime { get; set; }

	public bool IsHDR { get; set; }

	public CameraGetDataParam()
	{
		Gain = -1f;
		NDPort = -1;
		ImageSaveBpp = 16;
		FlipMode = CVImageFlipMode.None;
	}
}
