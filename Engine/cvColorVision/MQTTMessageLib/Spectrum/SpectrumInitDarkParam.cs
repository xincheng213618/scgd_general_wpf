namespace MQTTMessageLib.Spectrum;

public struct SpectrumInitDarkParam
{
	public bool AutoInitDark { get; set; }

	public float IntegralTime { get; set; }

	public int NumberOfAverage { get; set; }
}
