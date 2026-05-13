namespace MQTTMessageLib.FileServer;

public class DeviceFileServerUpload : DeviceCVBaseRequest<FileServerRequestType, FileUpDownParam>, IDevFileServerRequest, IDeviceRequest
{
	public DeviceFileServerUpload(string deviceName, string serialNumber, FileUpDownParam param)
		: base(deviceName, serialNumber, FileServerRequestType.UploadFile, param)
	{
	}
}
