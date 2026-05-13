namespace MQTTMessageLib.FileServer;

public class MQTTFileServerDownload : MQTTCVBaseRequest<FileUpDownParam>
{
	public MQTTFileServerDownload(string serviceName, string deviceName, FileUpDownParam data)
		: base(serviceName, deviceName, "File_Download", data)
	{
	}
}
