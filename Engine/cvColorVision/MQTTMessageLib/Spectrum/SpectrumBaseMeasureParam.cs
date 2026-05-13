namespace MQTTMessageLib.Spectrum;

public class SpectrumBaseMeasureParam
{
	public float IntegralTime { get; set; }

	public int NumberOfAverage { get; set; }

	public bool AutoIntegration { get; set; }

	public bool SelfAdaptionInitDark { get; set; }

	public bool AutoInitDark { get; set; }

	public int NDPort { get; set; }

	public bool IsWithND { get; set; }

	public string OutputDataFilename { get; set; }

	public SpectrumBaseMeasureParam()
	{
	}

	public SpectrumBaseMeasureParam(SpectrumMeasureParam param)
	{
		NumberOfAverage = param.NumberOfAverage;
		AutoIntegration = param.AutoIntegration;
		AutoInitDark = param.AutoInitDark;
		NDPort = param.NDPort;
		OutputDataFilename = param.OutputDataFilename;
		IntegralTime = param.IntegralTime;
		IsWithND = param.IsWithND;
		SelfAdaptionInitDark = param.SelfAdaptionInitDark;
	}
}
