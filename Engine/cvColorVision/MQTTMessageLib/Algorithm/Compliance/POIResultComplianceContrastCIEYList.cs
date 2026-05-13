using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultComplianceContrastCIEYList : POIResultComplianceContrastCIEList<POIResultDataCIEY>
{
	public POIResultComplianceContrastCIEYList(List<ComplianceResultOnlyDataCIE<POIResultDataCIEY>> data1, List<ComplianceResultOnlyDataCIE<POIResultDataCIEY>> data2, OperationType operationType)
		: base(data1, data2, operationType)
	{
		ContrastMath = new CVContrastMathCIE_Y();
	}

	protected override bool GetResultData(POIResultDataCIEY cie, MetricsResultDataType cieType, out float value)
	{
		bool result = false;
		value = 0f;
		if (cieType == MetricsResultDataType.CIE_Y)
		{
			value = cie.Y;
			result = true;
		}
		return result;
	}
}
