using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

public class CameraData
{
	public bool EnableFocus { get; set; }

	public int Focus { get; set; }

	public float Aperture { get; set; }

	public float Gain { get; set; }

	public int AvgCount { get; set; }

	public CVImageFlipMode FlipMode { get; set; }

	public float[] ExpTime { get; set; }

	public CVTemplateParam ExpTimeTemplate { get; set; }

	public CVTemplateParam AutoExpTimeTemplate { get; set; }

	public CVTemplateParam Calibration { get; set; }

	public CameraData(CVImageFlipMode flipMode, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName)
	{
		EnableFocus = enableFocus;
		Focus = focus;
		Aperture = aperture;
		Gain = gain;
		AvgCount = avgCount;
		ExpTime = expTime;
		FlipMode = flipMode;
		Calibration = new CVTemplateParam
		{
			ID = -1,
			Name = caliTempName
		};
	}
}
