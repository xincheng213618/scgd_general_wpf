namespace FlowEngineLib.Node.Spectrum;

public class SpectrumParamData
{
	public float IntegralTime { get; set; }

	public int NumberOfAverage { get; set; }

	public bool AutoIntegration { get; set; }

	public bool SelfAdaptionInitDark { get; set; }

	public bool AutoInitDark { get; set; }

	public bool IsWithND { get; set; }

	public SMUResultData SMUData { get; set; }
}
