namespace MQTTMessageLib.Algorithm;

public struct BlackMuraResult
{
	public double lv_avg { get; set; }

	public double lv_max { get; set; }

	public int max_pt_x { get; set; }

	public int max_pt_y { get; set; }

	public double lv_min { get; set; }

	public int min_pt_x { get; set; }

	public int min_pt_y { get; set; }

	public double uniformity { get; set; }

	public double za_rel_max { get; set; }

	public int Nle { get; set; }
}
