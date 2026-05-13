namespace MQTTMessageLib.Spectrum;

public struct SpectrumSysParam
{
	public int iFilterBW;

	public bool IsSyncFrequencyEnabled;

	public double Syncfreq;

	public int SyncfreqFactor;

	public float fSetWL1;

	public float fSetWL2;

	public float MaxIntegralTime { get; set; }

	public float BeginIntegralTime { get; set; }
}
