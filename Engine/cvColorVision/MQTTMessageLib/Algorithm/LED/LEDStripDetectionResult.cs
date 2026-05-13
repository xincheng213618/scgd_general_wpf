namespace MQTTMessageLib.Algorithm.LED;

public struct LEDStripDetectionResult
{
	public int PosX;

	public int PosY;

	public LEDStripDetectionResult(int x, int y)
	{
		this = default(LEDStripDetectionResult);
		PosX = x;
		PosY = y;
	}
}
