namespace CVCommCore.CVAlgorithm;

public class POIHeaderInfo
{
	public POIPointTypes PointType { get; set; }

	public int Rows { get; set; }

	public int Cols { get; set; }

	public float Width { get; set; }

	public float Height { get; set; }

	public float Angle { get; set; }

	public bool IsTotalLen(int len)
	{
		return Cols * Rows == len;
	}
}
