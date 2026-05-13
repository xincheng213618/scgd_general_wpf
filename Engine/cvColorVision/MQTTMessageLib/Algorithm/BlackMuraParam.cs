namespace MQTTMessageLib.Algorithm;

public struct BlackMuraParam
{
	public float aa_threshold { get; set; }

	public int erode_size { get; set; }

	public int min_aa_area { get; set; }

	public int aa_cut { get; set; }

	public int display_w { get; set; }

	public int display_h { get; set; }

	public double aa_size_w { get; set; }

	public double aa_size_h { get; set; }

	public int m_de { get; set; }

	public int n_de { get; set; }

	public bool rotate { get; set; }

	public int poi_num_x { get; set; }

	public int poi_num_y { get; set; }

	public int poi_type { get; set; }
}
