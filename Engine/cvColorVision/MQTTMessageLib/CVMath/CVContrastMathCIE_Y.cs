using MQTTMessageLib.Algorithm;
using MQTTMessageLib.Algorithm.Compliance;

namespace MQTTMessageLib.CVMath;

public class CVContrastMathCIE_Y : CVContrastMath<ComplianceResultOnlyDataCIE<POIResultDataCIEY>>
{
	protected override ComplianceResultOnlyDataCIE<POIResultDataCIEY> DoDivideItem(ComplianceResultOnlyDataCIE<POIResultDataCIEY> d1, ComplianceResultOnlyDataCIE<POIResultDataCIEY> d2)
	{
		POIResultDataCIEY cIEResult = DoDivideItemY(d1.CIEResult, d2.CIEResult);
		return new ComplianceResultOnlyDataCIE<POIResultDataCIEY>
		{
			CIEResult = cIEResult,
			DataType = d1.DataType,
			Name = $"{d1.Name}/{d2.Name}"
		};
	}

	private POIResultDataCIEY DoDivideItemY(POIResultDataCIEY d1, POIResultDataCIEY d2)
	{
		return new POIResultDataCIEY(d1.Y / d2.Y);
	}

	protected override ComplianceResultOnlyDataCIE<POIResultDataCIEY> DoSubtractItem(ComplianceResultOnlyDataCIE<POIResultDataCIEY> d1, ComplianceResultOnlyDataCIE<POIResultDataCIEY> d2)
	{
		POIResultDataCIEY cIEResult = DoSubtractItemY(d1.CIEResult, d2.CIEResult);
		return new ComplianceResultOnlyDataCIE<POIResultDataCIEY>
		{
			CIEResult = cIEResult,
			DataType = d1.DataType,
			Name = $"{d1.Name}-{d2.Name}"
		};
	}

	private POIResultDataCIEY DoSubtractItemY(POIResultDataCIEY d1, POIResultDataCIEY d2)
	{
		return new POIResultDataCIEY(d1.Y - d2.Y);
	}
}
