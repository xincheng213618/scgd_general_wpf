namespace CVCommCore.CVAlgorithm;

public struct POIFilterItem
{
	public POIFilterType FType { get; set; }

	public bool ThresholdUsePercent { get; set; }

	public float MaxPercent { get; set; }

	public float Threshold { get; set; }

	public int XYZType { get; set; }

	public static POIFilterItem Disable()
	{
		return new POIFilterItem
		{
			FType = POIFilterType.None
		};
	}
}
