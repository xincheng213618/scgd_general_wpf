namespace FlowEngineLib.Algorithm;

public class ProcessGlobalVariables
{
	public string Name { get; set; }

	public DeviceFlowInentify FlowInentify { get; set; }

	public int PreFlowRecorderId { get; set; }

	public ProcessGlobalVariables()
	{
		PreFlowRecorderId = -1;
	}

	public ProcessGlobalVariables(int mid)
	{
		PreFlowRecorderId = mid;
	}
}
