namespace MQTTMessageLib;

public class MQTTNodeServiceAdminHeader : MQTTNodeServiceHeader
{
	public string Authorization { get; set; }

	public string CallbackTopic { get; set; }

	public string AppId { get; set; }

	public bool IsAuthorization(string auth)
	{
		if (Authorization != null)
		{
			return Authorization.Equals(auth);
		}
		return false;
	}
}
