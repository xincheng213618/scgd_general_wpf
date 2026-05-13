namespace MQTTMessageLib.Algorithm;

public struct SfrRoiParam
{
	public float th { get; set; }

	public float lowThreshold { get; set; }

	public float highThreshold { get; set; }

	public float minLength { get; set; }

	public int roi_w { get; set; }

	public int roi_h { get; set; }
}
