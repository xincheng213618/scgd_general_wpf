using System.Collections.Generic;

namespace MQTTMessageLib.FileServer;

public class DeviceListAllFilesParam
{
	public FileExtType FileExtType { get; set; }

	public List<string> Files { get; set; }

	public DeviceListAllFilesParam(FileExtType fileExtType, List<string> files)
	{
		FileExtType = fileExtType;
		Files = files;
	}
}
