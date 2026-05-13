using System.Collections.Generic;

namespace CVCommCore.CVAlgorithm.CVArchived;

public class ComplianceCIEData<T>
{
	public string Name { get; set; }

	public string DataType { get; set; }

	public T Data { get; set; }

	public List<ComplianceRuleResult> DataComplianceResults { get; set; }
}
