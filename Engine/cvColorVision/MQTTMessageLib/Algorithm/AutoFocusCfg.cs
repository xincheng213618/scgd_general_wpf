namespace MQTTMessageLib.Algorithm;

public struct AutoFocusCfg
{
	public double forwardparam;

	public double curtailparam;

	public int curStep;

	public int stopStep;

	public int minPosition;

	public int maxPosition;

	public EvaFunc eEvaFunc;

	public double dMinValue;
}
