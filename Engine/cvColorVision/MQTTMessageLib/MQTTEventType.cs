namespace MQTTMessageLib;

public enum MQTTEventType
{
	Event_Resource_PhysicalCamera_Load = 0,
	Event_Camera_GetAllID = 100,
	Event_Camera_Open = 101,
	Event_Camera_OpenLive = 102,
	Event_Camera_Close = 103,
	Event_Camera_GetData = 104,
	Event_Camera_SetParam = 105,
	Event_Camera_GetAutoExpTime = 106,
	Event_Camera_DeleteData = 107,
	Event_Camera_Motor_AutoFocus = 108,
	Event_Camera_Motor_GetPosition = 109,
	Event_Camera_Motor_GoHome = 110,
	Event_Camera_Motor_Move = 111,
	Event_Camera_Motor_MoveDiaphragm = 112
}
