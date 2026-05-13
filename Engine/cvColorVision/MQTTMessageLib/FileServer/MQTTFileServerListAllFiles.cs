namespace MQTTMessageLib.FileServer;

public class MQTTFileServerListAllFiles : MQTTCVBaseRequest<ListAllFilesParam>
{
	public MQTTFileServerListAllFiles(string serviceName, string deviceName, ListAllFilesParam data)
		: base(serviceName, deviceName, "File_ListAll", data)
	{
	}
}
