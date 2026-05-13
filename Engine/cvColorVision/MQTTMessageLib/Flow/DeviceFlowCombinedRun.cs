namespace MQTTMessageLib.Flow;

public class DeviceFlowCombinedRun<T> : DeviceFlowBaseRequest<DeviceFlowCombinedRunParam<T>>
{
	public DeviceFlowCombinedRuntime<T>[] Runtimes { get; set; }

	public DeviceFlowCombinedRun(string deviceCode, string serialNumber, DeviceFlowCombinedRunParam<T> param)
		: base(deviceCode, serialNumber, FlowRequestType.CombinedRun, param)
	{
	}
}
