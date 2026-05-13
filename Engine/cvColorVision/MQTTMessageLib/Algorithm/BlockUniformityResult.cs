namespace MQTTMessageLib.Algorithm;

public struct BlockUniformityResult
{
	public double minLv { get; set; }

	public double maxLv { get; set; }

	public double avgLv { get; set; }

	private double lvUniformity { get; set; }
}
