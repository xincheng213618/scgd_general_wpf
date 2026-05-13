using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class DeviceComplianceContrastCIExyzResponse : DeviceComplianceMathResponse<POIResultComplianceContrastCIEXYZList>
{
	public DeviceComplianceContrastCIExyzResponse(string _InputParam, string _ComplianceTemplateName, CVBaseDeviceResponse status, POIResultComplianceContrastCIEXYZList data, long totalTime)
		: base(AlgorithmResultType.Compliance_Contrast_CIE_XYZ, _InputParam, _ComplianceTemplateName, data, status, totalTime)
	{
	}

	public static DeviceComplianceContrastCIExyzResponse Success(string _InputParam, string _ComplianceTemplateName, POIResultComplianceContrastCIEXYZList data, long totalTime)
	{
		return new DeviceComplianceContrastCIExyzResponse(_InputParam, _ComplianceTemplateName, CVBaseDeviceResponse.Success(), data, totalTime);
	}
}
