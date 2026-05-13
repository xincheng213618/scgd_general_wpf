using System;

namespace CVCommCore.CVImage;

public struct HImage
{
	public uint nWidth;

	public uint nHeight;

	public uint nChannels;

	public uint nBpp;

	public IntPtr pData;
}
