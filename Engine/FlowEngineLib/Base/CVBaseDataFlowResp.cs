using System;

namespace FlowEngineLib.Base;

public class CVBaseDataFlowResp
{
	public int Code { get; set; }

	public string Message { get; set; }

	public string Version { get; set; }

	public string ServiceName { get; set; }

	public string EventName { get; set; }

	public string SerialNumber { get; set; }

	public string MsgID { get; set; }

	public object Data { get; set; }

	public DateTime SendTime { get; set; }

	public int ZIndex { get; set; }

	public CVBaseDataFlowResp()
		: this(null, null)
	{
	}

	public CVBaseDataFlowResp(string serviceName, string eventName)
		: this(serviceName, eventName, null)
	{
	}

	public CVBaseDataFlowResp(string serviceName, string eventName, string sn)
		: this(serviceName, eventName, sn, null)
	{
	}

	public CVBaseDataFlowResp(string serviceName, string eventName, string sn, object data)
		: this("1.0", serviceName, eventName, sn, data)
	{
	}

	public CVBaseDataFlowResp(string version, string serviceName, string eventName, string sn, object data)
	{
		MsgID = Guid.NewGuid().ToString();
		SendTime = DateTime.Now;
		ServiceName = serviceName;
		Version = version;
		EventName = eventName;
		SerialNumber = sn;
		Data = data;
	}
}
