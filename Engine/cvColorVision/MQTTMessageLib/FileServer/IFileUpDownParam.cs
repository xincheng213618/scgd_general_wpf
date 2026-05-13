namespace MQTTMessageLib.FileServer;

public interface IFileUpDownParam
{
	FileExtType FileExtType { get; }

	string FileName { get; }

	string MD5 { get; }
}
