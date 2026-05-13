namespace MQTTMessageLib.Algorithm.Compliance;

public class DeviceComplianceContrastRequest : DeviceAlgorithmBaseRequest<ComplianceContrastParam>
{
	public DeviceComplianceContrastRequest(string deviceName, string serialNumber, int zIndex, ComplianceContrastParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.Compliance_Contrast, param)
	{
	}
}
