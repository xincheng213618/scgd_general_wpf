namespace MQTTMessageLib.FileServer;

public class MQTTFileServerUpload : MQTTCVBaseRequest<FileUpDownParam>
{
	public MQTTFileServerUpload(string serviceName, string deviceName, FileUpDownParam data)
		: base(serviceName, deviceName, "File_Upload", data)
	{
	}
}
