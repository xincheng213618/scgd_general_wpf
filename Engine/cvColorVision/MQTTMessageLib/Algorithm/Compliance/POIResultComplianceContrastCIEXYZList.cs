using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public class POIResultComplianceContrastCIEXYZList : POIResultComplianceContrastCIEList<POIResultDataCIExyuv>
{
	public POIResultComplianceContrastCIEXYZList(List<ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>> data1, List<ComplianceResultOnlyDataCIE<POIResultDataCIExyuv>> data2, OperationType operationType)
		: base(data1, data2, operationType)
	{
		ContrastMath = new CVContrastMathCIE_XYZ();
	}

	protected override bool GetResultData(POIResultDataCIExyuv cie, MetricsResultDataType cieType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (cieType)
		{
		case MetricsResultDataType.CIE_x:
			value = cie.x;
			result = true;
			break;
		case MetricsResultDataType.CIE_y:
			value = cie.y;
			result = true;
			break;
		case MetricsResultDataType.CIE_u:
			value = cie.u;
			result = true;
			break;
		case MetricsResultDataType.CIE_v:
			value = cie.v;
			result = true;
			break;
		case MetricsResultDataType.CIE_lv:
		case MetricsResultDataType.CIE_Y:
			value = cie.Y;
			result = true;
			break;
		case MetricsResultDataType.CIE_X:
			value = cie.X;
			result = true;
			break;
		case MetricsResultDataType.CIE_Z:
			value = cie.Z;
			result = true;
			break;
		case MetricsResultDataType.CCT:
			value = cie.CCT;
			result = true;
			break;
		case MetricsResultDataType.Wave:
			value = cie.Wave;
			result = true;
			break;
		}
		return result;
	}
}
