namespace CVCommCore.CVSpectrum;

public struct SpectrumColorData
{
	public int ID;

	public COLOR_PARA Data;

	public CieDataEx CIEDataEx;

	public float IntegralTime { get; set; }

	public SpectrumColorData(int id, float integralTime, COLOR_PARA data)
	{
		ID = id;
		Data = data;
		IntegralTime = integralTime;
		CIEDataEx = new CieDataEx(data);
	}
}
