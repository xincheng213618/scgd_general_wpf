namespace FlowEngineLib.Base;

internal class CommonEventData
{
	public string SerialNumber;

	public string TempName;

	public CommonEventData(string sn, string tpname)
	{
		SerialNumber = sn;
		TempName = tpname;
	}
}
