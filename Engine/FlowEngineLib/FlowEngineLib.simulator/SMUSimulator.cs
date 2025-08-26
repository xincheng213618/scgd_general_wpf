using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using Newtonsoft.Json;

namespace FlowEngineLib.simulator;

internal class SMUSimulator : BaseSimulator
{
	public SMUSimulator()
		: base("源表模拟器", "SMU", "DEV01")
	{
	}

	protected override CVMQTTRequest GetResponseEvent(MQActionEvent msg)
	{
		CVMQTTRequest result = null;
		if ("StartMeasureData".Equals(msg.EventName))
		{
			CVMQTTRequest cVMQTTRequest = JsonConvert.DeserializeObject<CVMQTTRequest>(msg.Message);
			CVServerResponse data = new CVServerResponse(cVMQTTRequest.MsgID, ActionStatusEnum.Finish, "", cVMQTTRequest.EventName, cVMQTTRequest.Data);
			result = new CVMQTTRequest(nodeCode, deviceCode, "Finish", cVMQTTRequest.SerialNumber, data, string.Empty);
		}
		else if ("SetMeasureData".Equals(msg.EventName))
		{
			CVMQTTRequest cVMQTTRequest2 = JsonConvert.DeserializeObject<CVMQTTRequest>(msg.Message);
			CVServerResponse data2 = new CVServerResponse(cVMQTTRequest2.MsgID, ActionStatusEnum.Finish, "", cVMQTTRequest2.EventName, cVMQTTRequest2.Data);
			result = new CVMQTTRequest(nodeCode, deviceCode, "Finish", cVMQTTRequest2.SerialNumber, data2, string.Empty);
		}
		return result;
	}
}
