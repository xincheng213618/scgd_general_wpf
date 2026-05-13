namespace CVCommCore.CVAlgorithm;

public class CVKBResultY
{
	public uint PixNumber { get; set; }

	public float Y { get; set; }

	public KeyBizInfo BizInfo { get; set; }

	public virtual void ValueCheck()
	{
		if (PixNumber == 0)
		{
			Y = -1f;
		}
	}
}
