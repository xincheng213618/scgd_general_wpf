using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

public class CVAOIBVCameraParam
{
	public float Gain { get; set; }

	public int AvgCount { get; set; }

	public CVImageFlipMode FlipMode { get; set; }

	public float[] ExpTime { get; set; }

	public bool IsAutoExpWithND { get; set; }

	public bool IsAutoExpTime { get; set; }

	public CVTemplateParam AutoExpTimeTemplate { get; set; }

	public CVTemplateParam Calibration { get; set; }

	public int ImageSaveBpp { get; set; }

	public string ImageOutName { get; set; }

	public CVAOIBVCameraParam(string name, float gain, int avgCount, CVImageFlipMode flipMode, float[] expTime, bool isAutoExpWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, int imageSaveBpp)
	{
		ImageOutName = name;
		Gain = gain;
		AvgCount = avgCount;
		FlipMode = flipMode;
		ExpTime = expTime;
		IsAutoExpWithND = isAutoExpWithND;
		IsAutoExpTime = isAutoExpTime;
		AutoExpTimeTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = autoExpTempName
		};
		Calibration = new CVTemplateParam
		{
			ID = -1,
			Name = caliTempName
		};
		ImageSaveBpp = imageSaveBpp;
	}
}
