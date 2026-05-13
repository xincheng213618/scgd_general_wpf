namespace CVCommCore.CVTPA;

public class TPAResultValue
{
	public string RType { get; set; }

	public string RVal { get; set; }

	public TPAResultValue(string rType, string rVal)
	{
		RType = rType;
		RVal = rVal;
	}
}
