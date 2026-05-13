using System.Runtime.InteropServices;

namespace CVCommCore.CVSpectrum;

public struct COLOR_PARA
{
	public float fCIEx;

	public float fCIEy;

	public float fCIEz;

	public float fCIEx_2015;

	public float fCIEy_2015;

	public float fCIEz_2015;

	public float fx;

	public float fy;

	public float fu;

	public float fv;

	public float fx_2015;

	public float fy_2015;

	public float fu_2015;

	public float fv_2015;

	public float fCCT;

	public float dC;

	public float fLd;

	public float fPur;

	public float fLp;

	public float fHW;

	public float fLav;

	public float fRa;

	public float fRR;

	public float fGR;

	public float fBR;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
	public float[] fRi;

	public float fIp;

	public float fPh;

	public float fPhe;

	public float fPlambda;

	public float fSpect1;

	public float fSpect2;

	public float fInterval;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10000)]
	public float[] fPL;
}
