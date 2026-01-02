namespace FlowEngineLib.Node.Spectrum;

public class SpectrumEQEParamData
{
	public float IntegralTime { get; set; }

	public int NumberOfAverage { get; set; }

	public bool AutoIntegration { get; set; }

	public bool SelfAdaptionInitDark { get; set; }

	public bool AutoInitDark { get; set; }

	public string OutputDataFilename { get; set; }

	public float Divisor { get; set; } = 1f;

	public SMUResultData SMUData { get; set; }
}
