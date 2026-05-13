namespace CVCommCore.CVAlgorithm;

public struct POIFilter
{
	public bool ThresholdUsePercent { get; set; }

	public bool Enable { get; set; }

	public bool NoAreaEnable { get; set; }

	public bool XYZEnable { get; set; }

	public float Threshold { get; set; }

	public int XYZType { get; set; }
}
