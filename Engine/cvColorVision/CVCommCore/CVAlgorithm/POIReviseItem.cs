namespace CVCommCore.CVAlgorithm;

public struct POIReviseItem
{
	public GenReviseType ReviseType { get; set; }

	public float m { get; set; }

	public float n { get; set; }

	public float p { get; set; }

	public POIReviseItem(POIReviseItem src)
	{
		this = default(POIReviseItem);
		ReviseType = src.ReviseType;
		m = src.m;
		n = src.n;
		p = src.p;
	}

	public POIReviseItem(POIFileReviseItem src)
	{
		this = default(POIReviseItem);
		ReviseType = src.GenCalibrationType;
		m = src.M;
		n = src.N;
		p = src.P;
	}

	public void GetMNPValue(ref bool enable, ref float m, ref float n, ref float p)
	{
		switch (ReviseType)
		{
		case GenReviseType.None:
			enable = false;
			break;
		case GenReviseType.ChromaOnly:
			m = this.m / this.n;
			n = 1f;
			p = this.p / this.n;
			enable = true;
			break;
		case GenReviseType.BrightnessOnly:
			m = this.n;
			n = this.n;
			p = this.n;
			enable = true;
			break;
		case GenReviseType.BrightnessAndChroma:
			m = this.m;
			n = this.n;
			p = this.p;
			enable = true;
			break;
		default:
			enable = false;
			break;
		}
	}

	public static POIReviseItem Disable()
	{
		return new POIReviseItem
		{
			ReviseType = GenReviseType.None,
			m = 1f,
			n = 1f,
			p = 1f
		};
	}
}
