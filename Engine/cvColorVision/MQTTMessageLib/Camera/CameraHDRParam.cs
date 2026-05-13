namespace MQTTMessageLib.Camera;

public struct CameraHDRParam
{
	public float[] ExpTimes { get; set; }

	public float ThHigh { get; set; }

	public float ThLow { get; set; }

	public float HDRExpTime { get; set; }

	public float Gain { get; set; }

	public uint AvgCount { get; set; }
}
