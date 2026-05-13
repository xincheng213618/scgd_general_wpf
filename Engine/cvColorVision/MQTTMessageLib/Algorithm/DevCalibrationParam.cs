namespace MQTTMessageLib.Algorithm;

public struct DevCalibrationParam(bool enable)
{
	public string FileName_DarkNoise = "";

	public bool Selected_DarkNoise = enable;

	public string FileName_DSNU = "";

	public bool Selected_DSNU = enable;

	public string FileName_Distortion = "";

	public bool Selected_Distortion = enable;

	public string FileName_DefectWPoint = "";

	public bool Selected_DefectWPoint = enable;

	public string FileName_DefectBPoint = "";

	public bool Selected_DefectBPoint = enable;

	public string FileName_Luminance = "";

	public bool Selected_Luminance = enable;

	public string FileName_ColorOne = "";

	public bool Selected_ColorOne = enable;

	public string FileName_ColorFour = "";

	public bool Selected_ColorFour = enable;

	public string FileName_ColorMulti = "";

	public bool Selected_ColorMulti = enable;

	public string FileName_Uniformity_Y = "";

	public bool Selected_Uniformity_Y = enable;

	public string FileName_Uniformity_Z = "";

	public bool Selected_Uniformity_Z = enable;

	public string FileName_Uniformity_X = "";

	public bool Selected_Uniformity_X = enable;

	public bool enable = enable;
}
