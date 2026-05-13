namespace MQTTMessageLib;

public class MQTTNodeServiceTokenHeader : MQTTNodeServiceHeader
{
	public string Token { get; set; }

	public MQTTNodeServiceTokenHeader(string nodeName, string serviceType, string eventName, string token)
		: base(nodeName, serviceType, eventName)
	{
		Token = token;
	}

	public MQTTNodeServiceTokenHeader()
	{
	}

	public bool TokenCheck(string accessToken)
	{
		string version = base.Version;
		if (version == null || version.Equals("1.0"))
		{
			return true;
		}
		if (!string.IsNullOrWhiteSpace(Token))
		{
			return Token.Equals(accessToken);
		}
		return false;
	}
}
