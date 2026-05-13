namespace CVCommCore.CVSpectrum;

public struct CieDataEx(COLOR_PARA clrData)
{
	public float fCIEx = clrData.fCIEx;

	public float fCIEy = clrData.fCIEy;

	public float fCIEz = clrData.fCIEz;

	public float fCIEx_2015 = clrData.fCIEx_2015;

	public float fCIEy_2015 = clrData.fCIEy_2015;

	public float fCIEz_2015 = clrData.fCIEz_2015;

	public float fx_2015 = clrData.fx_2015;

	public float fy_2015 = clrData.fy_2015;

	public float fu_2015 = clrData.fu_2015;

	public float fv_2015 = clrData.fv_2015;
}
