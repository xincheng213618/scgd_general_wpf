using System.Runtime.InteropServices;

namespace MQTTMessageLib.Algorithm;

public struct POIPointCIE
{
	[MarshalAs(UnmanagedType.U1)]
	public byte poiType;

	public int poiX;

	public int poiY;

	public int poiWidth;

	public int poiHeight;

	public bool hasValue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
	public double[] poiValue;
}
