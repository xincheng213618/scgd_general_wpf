namespace FlowEngineLib.Algorithm;

public class ComplianceContrastParam : AlgorithmBaseParam
{
	public ProcessGlobalVariables InputParam1 { get; set; }

	public ProcessGlobalVariables InputParam2 { get; set; }

	public int OperationType { get; set; }

	public ComplianceContrastParam()
	{
		InputParam1 = new ProcessGlobalVariables();
		InputParam2 = new ProcessGlobalVariables();
	}

	public ComplianceContrastParam(int operation, int masterId1, int masterId2)
		: this()
	{
		OperationType = operation;
		InputParam1.PreFlowRecorderId = masterId1;
		InputParam2.PreFlowRecorderId = masterId2;
	}
}
