namespace MQTTMessageLib.Algorithm.Compliance;

public class DeviceComplianceMathRequest : DeviceAlgorithmBaseRequest<DeviceComplianceMathParam>
{
	public DeviceComplianceMathRequest(string deviceName, string serialNumber, int zIndex, DeviceComplianceMathParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.ComplianceMath, param)
	{
	}
}
