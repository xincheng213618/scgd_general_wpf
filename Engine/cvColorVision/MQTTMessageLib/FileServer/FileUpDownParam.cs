namespace MQTTMessageLib.FileServer;

public class FileUpDownParam : IFileUpDownParam
{
	public string DeviceCode { get; set; }

	public string DeviceType { get; set; }

	public string FileName { get; set; }

	public string FileURL { get; set; }

	public string MD5 { get; set; }

	public FileExtType FileExtType { get; set; }

	public FileUpDownParam()
	{
	}

	public FileUpDownParam(string fileName, FileExtType fileExtType)
	{
		FileName = fileName;
		FileExtType = fileExtType;
		FileURL = string.Empty;
	}
}
