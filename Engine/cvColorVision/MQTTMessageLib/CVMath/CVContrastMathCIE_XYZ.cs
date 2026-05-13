using MQTTMessageLib.Algorithm;
using MQTTMessageLib.Algorithm.Compliance;

namespace MQTTMessageLib.CVMath;

public class CVContrastMathCIE_XYZ : CVContrastMath<ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>>
{
	private POIResultDataCIExyuv DoSubtractItemXYZ(POIResultDataCIExyuv d1, POIResultDataCIExyuv d2)
	{
		return new POIResultDataCIExyuv
		{
			CCT = d1.CCT - d2.CCT,
			Wave = d1.Wave - d2.Wave,
			X = d1.X - d2.X,
			Y = d1.Y - d2.Y,
			Z = d1.Z - d2.Z,
			x = d1.x - d2.x,
			y = d1.y - d2.y,
			u = d1.u - d2.u,
			v = d1.v - d2.v
		};
	}

	protected override ComplianceResultOnlyDataCIE<POIResultDataCIExyuv> DoSubtractItem(ComplianceResultOnlyDataCIE<POIResultDataCIExyuv> d1, ComplianceResultOnlyDataCIE<POIResultDataCIExyuv> d2)
	{
		POIResultDataCIExyuv cIEResult = DoSubtractItemXYZ(d1.CIEResult, d2.CIEResult);
		return new ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>
		{
			CIEResult = cIEResult,
			DataType = d1.DataType,
			Name = $"{d1.Name}-{d2.Name}"
		};
	}

	protected override ComplianceResultOnlyDataCIE<POIResultDataCIExyuv> DoDivideItem(ComplianceResultOnlyDataCIE<POIResultDataCIExyuv> d1, ComplianceResultOnlyDataCIE<POIResultDataCIExyuv> d2)
	{
		POIResultDataCIExyuv cIEResult = DoDivideItemXYZ(d1.CIEResult, d2.CIEResult);
		return new ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>
		{
			CIEResult = cIEResult,
			DataType = d1.DataType,
			Name = $"{d1.Name}/{d2.Name}"
		};
	}

	private POIResultDataCIExyuv DoDivideItemXYZ(POIResultDataCIExyuv d1, POIResultDataCIExyuv d2)
	{
		return new POIResultDataCIExyuv
		{
			CCT = d1.CCT / d2.CCT,
			Wave = d1.Wave / d2.Wave,
			X = d1.X / d2.X,
			Y = d1.Y / d2.Y,
			Z = d1.Z / d2.Z,
			x = d1.x / d2.x,
			y = d1.y / d2.y,
			u = d1.u / d2.u,
			v = d1.v / d2.v
		};
	}
}
