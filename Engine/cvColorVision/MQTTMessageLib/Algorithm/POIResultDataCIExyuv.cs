namespace MQTTMessageLib.Algorithm;

public class POIResultDataCIExyuv
{
	public float CCT { get; set; }

	public float Wave { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float Z { get; set; }

	public float x { get; set; }

	public float y { get; set; }

	public float u { get; set; }

	public float v { get; set; }

	public POIResultDataCIExyuv()
	{
	}

	public POIResultDataCIExyuv(float CCT, float Wave, float X, float Y, float Z, float x, float y, float u, float v)
		: this()
	{
		this.CCT = CCT;
		this.Wave = Wave;
		this.X = X;
		this.Y = Y;
		this.Z = Z;
		this.x = x;
		this.y = y;
		this.u = u;
		this.v = v;
	}

}
