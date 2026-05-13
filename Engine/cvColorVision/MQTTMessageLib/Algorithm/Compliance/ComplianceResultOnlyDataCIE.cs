using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class ComplianceResultOnlyDataCIE<T>
{
	public string Name { get; set; }

	public DataMetricsModel DataType { get; set; }

	public T CIEResult { get; set; }
}
