namespace MQTTMessageLib.Algorithm.XR;

public struct FOVResult(FovPattern pattern, FovType type, double degrees)
{
	public FovPattern Pattern = pattern;

	public FovType Type = type;

	public double Degrees = degrees;
}
