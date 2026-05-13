using Newtonsoft.Json;

namespace MQTTMessageLib.Flow;

public class DeviceFlowCombinedRuntime<T>
{
	public string SNSuffixes { get; private set; }

	public string PSerialNumber { get; private set; }

	public string PName { get; private set; }

	public string FlowName { get; private set; }

	[JsonIgnore]
	public string FlowData { get; private set; }

	[JsonIgnore]
	public IDevFlowRequest Request { get; set; }

	[JsonIgnore]
	public IDevFlowResponse Response { get; set; }

	public DeviceFlowCombinedRuntime(CombinedFlowTempCfg flowTemp)
		: this(flowTemp.Name, flowTemp.FlowData, flowTemp.SNSuffixes)
	{
	}

	public DeviceFlowCombinedRuntime(string flowName, string flowData, string snSuffixes)
	{
		FlowName = flowName;
		FlowData = flowData;
		SNSuffixes = snSuffixes;
		PSerialNumber = string.Empty;
		PName = string.Empty;
		Request = null;
		Response = null;
	}

	public DeviceFlowCombinedRuntime(string sNSuffixes, string pSerialNumber, string pName, string flowName, string flowData, IDevFlowRequest request, IDevFlowResponse response)
		: this(flowName, flowData, sNSuffixes)
	{
		PSerialNumber = pSerialNumber;
		PName = pName;
		Request = request;
		Response = response;
	}

	public DeviceFlowCombinedRuntime<T> Clone()
	{
		return new DeviceFlowCombinedRuntime<T>(SNSuffixes, PSerialNumber, PName, FlowName, FlowData, Request, Response);
	}

	public void BuildReq(DeviceFlowCombinedRun<T> req)
	{
		ChangeProduct(req.Params.Name, req.SerialNumber);
		BuildReq(req.DeviceCode);
	}

	public void BuildReq(string deviceCode)
	{
		Request = null;
		Response = null;
		if (!string.IsNullOrEmpty(PSerialNumber) && !string.IsNullOrEmpty(PName))
		{
			string serialNumber = $"{PSerialNumber}{SNSuffixes}";
			Request = new DeviceFlowRun<T>(deviceCode, serialNumber, new DeviceFlowRunParam<T>
			{
				Name = PName
			});
			Response = null;
		}
	}

	public void ChangeProduct(string PName, string PSerialNumber)
	{
		this.PSerialNumber = PSerialNumber;
		this.PName = PName;
	}

	public void ChangeProduct(DeviceFlowCombinedRuntime<T> product)
	{
		ChangeProduct(product.PName, product.PSerialNumber);
	}
}
