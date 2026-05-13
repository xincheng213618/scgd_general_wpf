namespace MQTTMessageLib;

public class MQTTCVResponseStatus
{
	public int Code { get; set; }

	public string Desc { get; set; }

	public MQTTCVResponseStatus(int code, string desc)
	{
		Code = code;
		Desc = desc;
	}

	public MQTTCVResponseStatus()
	{
	}

	public MQTTCVResponseStatus(IDeviceResponse response)
	{
		Code = response.Code;
		Desc = response.Desc;
	}
}
