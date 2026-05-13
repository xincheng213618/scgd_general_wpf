using System.Runtime.InteropServices;

namespace CVCommCore.CVSpectrum;

public struct COLOR_PARA_EQE
{
	public float fx;

	public float fy;

	public float fu;

	public float fv;

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

	public float fIp;

	public float fPh;

	public float fPhe;

	public float fPlambda;

	public float fSpect1;

	public float fSpect2;

	public float fInterval;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4001)]
	public float[] fPL;

	public float dIm;

	public double dW;

	public double dEqe;

	public double dVoltage;

	public double dCurrent;

	public double dP;
}
