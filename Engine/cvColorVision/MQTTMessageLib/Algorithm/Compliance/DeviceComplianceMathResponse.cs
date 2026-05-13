using CVCommCore;

namespace MQTTMessageLib.Algorithm.Compliance;

public class DeviceComplianceMathResponse<T> : DeviceAlgorithmBaseResponse
{
	public T Data { get; set; }

	public string InputParam { get; set; }

	public string TemplateName { get; set; }

	public DeviceComplianceMathResponse(AlgorithmResultType resultType, string _InputParam, string _ComplianceTemplateName, T result, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, status, totalTime)
	{
		InputParam = _InputParam;
		TemplateName = _ComplianceTemplateName;
		Data = result;
	}
}
