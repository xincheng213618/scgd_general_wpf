using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultComplianceCIEYExList : POIResultComplianceCIEList<POIResultDataCIEYEx>
{
	public POIResultComplianceCIEYExList(List<POIResultCIE<POIResultDataCIEYEx>> poi)
		: base(poi, (CVPOIMath<POIResultDataCIEYEx>)new CVMathCIE_YEx())
	{
	}

	protected override bool GetResultData(POIResultDataCIEYEx cie, MetricsResultDataType cieType, int idx, out float value)
	{
		return cie.GetResultData(cieType, idx, out value);
	}
}
