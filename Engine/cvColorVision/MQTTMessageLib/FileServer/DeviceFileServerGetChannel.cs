namespace MQTTMessageLib.FileServer;

public class DeviceFileServerGetChannel : DeviceCVBaseRequest<FileServerRequestType, GetChannelParam>, IDevFileServerRequest, IDeviceRequest
{
	public DeviceFileServerGetChannel(string deviceCode, string serialNumber, GetChannelParam param)
		: base(deviceCode, serialNumber, FileServerRequestType.GetChannel, param)
	{
	}
}
