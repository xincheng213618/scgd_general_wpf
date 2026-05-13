using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class ComplianceContrastParam : DeviceAlgorithmBaseInputParam
{
	public int OperationType { get; set; }

	public List<ValidateRule> Rules { get; set; }

	public ProcessGlobalVariables InputParam1 { get; set; }

	public ProcessGlobalVariables InputParam2 { get; set; }

	public List<ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>> ResultCIEXYZList1 { get; set; }

	public List<ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>> ResultCIEXYZList2 { get; set; }

	public List<ComplianceResultOnlyDataCIE<POIResultDataCIEY>> ResultCIEYList1 { get; set; }

	public List<ComplianceResultOnlyDataCIE<POIResultDataCIEY>> ResultCIEYList2 { get; set; }
}
