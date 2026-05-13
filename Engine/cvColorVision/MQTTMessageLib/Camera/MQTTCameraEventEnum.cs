namespace MQTTMessageLib.Camera;

public class MQTTCameraEventEnum
{
	public const string Event_GetAllID = "CM_GetAllSnID";

	public const string Event_GetID = "CM_GetSnID";

	public const string Event_Open = "Open";

	public const string Event_OpenLive = "OpenLive";

	public const string Event_Close = "Close";

	public const string Event_GetData = "GetData";

	public const string Event_GetDataAndAlgorithm = "GetDataAndAlgorithm";

	public const string Event_SetParam = "SetParam";

	public const string Event_GetAutoExpTime = "GetAutoExpTime";

	public const string Event_Motor_AutoFocus = "AutoFocus";

	public const string Event_Motor_GetPosition = "GetPosition";

	public const string Event_Motor_GoHome = "GoHome";

	public const string Event_Motor_Move = "Move";

	public const string Event_Motor_MoveDiaphragm = "MoveDiaphragm";

	public const string Event_Delete_Data = "DeleteData";

	public const string Event_SetNDPort = "SetPort";

	public const string Event_GetNDPort = "GetPort";
}
