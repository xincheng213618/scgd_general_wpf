using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultComplianceCIEYList : POIResultComplianceCIEList<POIResultDataCIEY>
{
	public POIResultComplianceCIEYList(List<POIResultCIE<POIResultDataCIEY>> poi)
		: base(poi, (CVPOIMath<POIResultDataCIEY>)new CVMathCIE_Y())
	{
	}

	protected override bool GetResultData(POIResultDataCIEY cie, MetricsResultDataType cieType, int idx, out float value)
	{
		return cie.GetResultData(cieType, out value);
	}
}
