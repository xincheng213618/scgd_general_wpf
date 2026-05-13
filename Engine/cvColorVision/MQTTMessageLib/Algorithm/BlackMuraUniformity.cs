namespace MQTTMessageLib.Algorithm;

public struct BlackMuraUniformity
{
	public double minLv { get; set; }

	public double maxLv { get; set; }

	public double avgLv { get; set; }

	public double lvUniformity { get; set; }

	public double[] localLvUniformity { get; set; }

	public int block_num_x { get; set; }

	public int block_num_y { get; set; }
}
