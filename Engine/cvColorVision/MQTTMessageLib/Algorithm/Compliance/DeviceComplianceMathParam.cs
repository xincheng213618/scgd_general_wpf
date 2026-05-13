using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class DeviceComplianceMathParam : DeviceAlgorithmBaseInputParam
{
	public string Name { get; set; }

	public ComplianceMathType ComplianceType { get; set; }

	public List<ValidateRule> Rules { get; set; }

	public Dictionary<string, List<ValidateRule>> BindRules { get; set; }

	public IComplianceMathImp ComplianceMath { get; set; }

	public bool IsBreak { get; set; }
}
