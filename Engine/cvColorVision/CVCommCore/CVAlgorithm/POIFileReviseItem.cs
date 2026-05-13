namespace CVCommCore.CVAlgorithm;

public struct POIFileReviseItem
{
	public int Id { get; set; }

	public string Name { get; set; }

	public int PixX { get; set; }

	public int PixY { get; set; }

	public int PixWidth { get; set; }

	public int PixHeight { get; set; }

	public GenReviseType GenCalibrationType { get; set; }

	public float M { get; set; }

	public float N { get; set; }

	public float P { get; set; }
}
