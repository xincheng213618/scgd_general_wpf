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

	public POITemplateParam POIParam { get; set; }

	public string GlobalVariableName { get; set; }

	public CameraData(CVImageFlipMode flipMode, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName, string globalVariableName)
	{
		EnableFocus = enableFocus;
		Focus = focus;
		Aperture = aperture;
		Gain = gain;
		AvgCount = avgCount;
		ExpTime = expTime;
		FlipMode = flipMode;
		if (!string.IsNullOrEmpty(poiTempName))
		{
			POIParam = new POITemplateParam(poiTempName, poiFilterTempName, poiReviseTempName);
		}
		Calibration = new CVTemplateParam
		{
			ID = -1,
			Name = caliTempName
		};
		GlobalVariableName = globalVariableName;
	}
}
