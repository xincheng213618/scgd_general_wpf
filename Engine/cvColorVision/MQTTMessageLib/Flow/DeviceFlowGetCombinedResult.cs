namespace MQTTMessageLib.Flow;

public class DeviceFlowGetCombinedResult<T> : DeviceFlowBaseRequest<CVTemplateParam>
{
	public DeviceFlowCombinedRuntime<T>[] Runtimes { get; set; }

	public DeviceFlowGetCombinedResult(string deviceCode, string serialNumber, CVTemplateParam param)
		: base(deviceCode, serialNumber, FlowRequestType.GetCombinedResult, param)
	{
	}
}
