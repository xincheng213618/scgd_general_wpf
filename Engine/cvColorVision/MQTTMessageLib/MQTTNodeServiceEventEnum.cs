namespace MQTTMessageLib;

public class MQTTNodeServiceEventEnum
{
	public const string Event_SetToken = "SetToken";

	public const string Event_Regist = "Regist";

	public const string Event_NotRegist = "NotRegist";

	public const string Event_Startup = "Startup";

	public const string Event_AddService = "AddService";

	public const string Event_StopService = "StopService";

	public const string Event_StopAllServices = "StopAllServices";

	public const string Event_LoadAllServices = "LoadAllServices";

	public const string Event_ReloadService = "ReloadService";

	public const string Event_QueryServices = "QueryServices";

	public const string Event_QueryServiceStatus = "QueryServiceStatus";

	public const string Event_ServiceHeartbeat = "ServiceHeartbeat";
}
