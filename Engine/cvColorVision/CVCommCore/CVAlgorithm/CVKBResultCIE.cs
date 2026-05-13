namespace CVCommCore.CVAlgorithm;

public class CVKBResultCIE : CVKBResultY
{
	public float CCT { get; set; }

	public float Wave { get; set; }

	public float X { get; set; }

	public float Z { get; set; }

	public float x { get; set; }

	public float y { get; set; }

	public float u { get; set; }

	public float v { get; set; }

	public override void ValueCheck()
	{
		if (base.PixNumber == 0)
		{
			X = -1f;
			base.Y = -1f;
			Z = -1f;
			x = -1f;
			y = -1f;
			u = -1f;
			v = -1f;
			CCT = -1f;
			Wave = -1f;
		}
	}
}
