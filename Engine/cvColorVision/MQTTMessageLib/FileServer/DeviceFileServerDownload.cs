namespace MQTTMessageLib.FileServer;

public class DeviceFileServerDownload : DeviceCVBaseRequest<FileServerRequestType, FileUpDownParam>, IDevFileServerRequest, IDeviceRequest
{
	public string TargetDevicePath { get; set; }

	public DeviceFileServerDownload(string deviceName, string serialNumber, FileUpDownParam param)
		: base(deviceName, serialNumber, FileServerRequestType.DownloadFile, param)
	{
	}
}
