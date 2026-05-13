namespace MQTTMessageLib.Algorithm.LED;

public struct LedCheckResult
{
	public float Radius;

	public int PosX;

	public int PosY;

	public LedCheckResult(float radius, int x, int y)
	{
		this = default(LedCheckResult);
		Radius = radius;
		PosX = x;
		PosY = y;
	}
}
