namespace MQTTMessageLib.FileServer;

public class DeviceFileUpdownParam
{
	public bool IsLocal { get; set; }

	public string FileName { get; set; }

	public string ServerEndpoint { get; set; }

	public FileExtType FileExtType { get; set; }

	public DeviceFileUpdownParam()
	{
	}

	public DeviceFileUpdownParam(DeviceFileUpdownParam param)
		: this(param.IsLocal, param.FileName, param.ServerEndpoint, param.FileExtType)
	{
	}

	public DeviceFileUpdownParam(bool isLocal, string fileName, string serverEndpoint, FileExtType fileExtType)
	{
		IsLocal = isLocal;
		FileName = fileName;
		ServerEndpoint = serverEndpoint;
		FileExtType = fileExtType;
	}
}
