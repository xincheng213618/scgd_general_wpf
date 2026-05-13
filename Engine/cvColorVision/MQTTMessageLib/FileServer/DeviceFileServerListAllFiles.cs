namespace MQTTMessageLib.FileServer;

public class DeviceFileServerListAllFiles : DeviceCVBaseRequest<FileServerRequestType, ListAllFilesParam>, IDevFileServerRequest, IDeviceRequest
{
	public string TargetDevicePath { get; set; }

	public DeviceFileServerListAllFiles(string deviceName, string serialNumber, ListAllFilesParam param)
		: base(deviceName, serialNumber, FileServerRequestType.GetAllFiles, param)
	{
	}
}
