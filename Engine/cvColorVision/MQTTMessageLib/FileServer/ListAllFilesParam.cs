namespace MQTTMessageLib.FileServer;

public struct ListAllFilesParam
{
	public FileExtType FileExtType { get; set; }

	public string DeviceCode { get; set; }

	public string DeviceType { get; set; }
}
