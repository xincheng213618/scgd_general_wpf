namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumEventEnum
{
	public const string Event_Shutter_Doopen = "ShutterDoOpen";

	public const string Event_Shutter_Doclose = "ShutterDoClose";

	public const string Event_Shutter_Connect = "ShutterConnect";

	public const string Event_Shutter_Disconnect = "ShutterDisconnect";

	public const string Event_Open = "Open";

	public const string Event_Close = "Close";

	public const string Event_GetData = "GetData";

	public const string Event_GetData_EQE = "EQE.GetData";

	public const string Event_GetData_Start = "StartGetData";

	public const string Event_GetData_Stop = "StopGetData";

	public const string Event_GetData_Auto_Start = "GetDataAuto";

	public const string Event_GetData_EQE_Auto_Start = "EQE.GetDataAuto";

	public const string Event_GetData_Auto_Stop = "GetDataAutoStop";

	public const string Event_InitDark = "InitDark";

	public const string Event_InitAutoDark = "InitAutoDark";

	public const string Event_GetParam = "GetParam";

	public const string Event_SetParam = "SetParam";

	public const string Event_Scan = "Scan";

	public const string Event_GetAllID = "CM_GetAllSnID";

	public const string Event_SetNDPort = "SetPort";

	public const string Event_GetNDPort = "GetPort";
}
