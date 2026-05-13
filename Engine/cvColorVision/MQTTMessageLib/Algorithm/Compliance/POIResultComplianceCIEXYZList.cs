using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultComplianceCIEXYZList : POIResultComplianceCIEList<POIResultDataCIExyuv>
{
	public POIResultComplianceCIEXYZList(List<POIResultCIE<POIResultDataCIExyuv>> poi)
		: base(poi, (CVPOIMath<POIResultDataCIExyuv>)new CVMathCIE_XYZ())
	{
	}

	protected override bool GetResultData(POIResultDataCIExyuv cie, MetricsResultDataType cieType, int idx, out float value)
	{
		return cie.GetResultData(cieType, out value);
	}
}
