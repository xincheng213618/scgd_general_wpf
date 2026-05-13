using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultDataComplianceCIE<T>
{
	public string Name { get; set; }

	public DataMetricsModel DataType { get; set; }

	public T CIEResult { get; set; }

	public List<ValidateRuleResult> ComplianceResults { get; set; }
}
