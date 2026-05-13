namespace MQTTMessageLib.Spectrum;

public struct SpectrumSelfAdaptionInitDark
{
	public float BeginIntegralTime { get; set; }

	public int NumberOfAverage { get; set; }

	public int StepTime { get; set; }

	public int StepCount { get; set; }
}
