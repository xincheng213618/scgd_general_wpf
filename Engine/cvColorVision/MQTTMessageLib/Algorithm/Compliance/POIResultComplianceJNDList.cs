using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultComplianceJNDList : POIResultComplianceCIEList<POIResultDataJND>
{
	public POIResultComplianceJNDList(List<POIResultCIE<POIResultDataJND>> poi)
		: base(poi, (CVPOIMath<POIResultDataJND>)new CVMathJND())
	{
	}

	protected override bool GetResultData(POIResultDataJND cie, MetricsResultDataType cieType, int idx, out float value)
	{
		return cie.GetResultData(cieType, out value);
	}
}
