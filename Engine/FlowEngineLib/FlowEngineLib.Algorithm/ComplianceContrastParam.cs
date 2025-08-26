namespace FlowEngineLib.Algorithm;

public class ComplianceContrastParam
{
	public ProcessGlobalVariables InputParam1 { get; set; }

	public ProcessGlobalVariables InputParam2 { get; set; }

	public int OperationType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public ComplianceContrastParam()
	{
		InputParam1 = new ProcessGlobalVariables();
		InputParam2 = new ProcessGlobalVariables();
		TemplateParam = new CVTemplateParam();
	}

	public ComplianceContrastParam(string tempName, int operation, int masterId1, int masterId2)
		: this()
	{
		TemplateParam.Name = tempName;
		OperationType = operation;
		InputParam1.PreFlowRecorderId = masterId1;
		InputParam2.PreFlowRecorderId = masterId2;
	}
}
