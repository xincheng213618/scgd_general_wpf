using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class DeviceComplianceContrastCIEYResponse : DeviceComplianceMathResponse<POIResultComplianceContrastCIEYList>
{
	public DeviceComplianceContrastCIEYResponse(string _POIMasterName, string _ComplianceTemplateName, CVBaseDeviceResponse status, POIResultComplianceContrastCIEYList data, long totalTime)
		: base(AlgorithmResultType.Compliance_Contrast_CIE_Y, _POIMasterName, _ComplianceTemplateName, data, status, totalTime)
	{
	}

	public static DeviceComplianceContrastCIEYResponse Success(string _InputParam, string _ComplianceTemplateName, POIResultComplianceContrastCIEYList data, long totalTime)
	{
		return new DeviceComplianceContrastCIEYResponse(_InputParam, _ComplianceTemplateName, CVBaseDeviceResponse.Success(), data, totalTime);
	}
}
