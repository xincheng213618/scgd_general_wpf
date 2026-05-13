namespace MQTTMessageLib.FileServer;

public class DeviceFileServerDownloadResponse : DeviceFileServerResponse
{
	public DeviceFileUpdownParam Data { get; set; }

	public DeviceFileServerDownloadResponse(CVBaseDeviceResponse status, DeviceFileUpdownParam data)
		: base(status)
	{
		Data = data;
	}

	public static DeviceFileServerDownloadResponse Success(DeviceFileUpdownParam data)
	{
		return new DeviceFileServerDownloadResponse(CVBaseDeviceResponse.Success(), data);
	}

	public static DeviceFileServerDownloadResponse Pending(DeviceFileUpdownParam data)
	{
		return new DeviceFileServerDownloadResponse(CVBaseDeviceResponse.Pending(), data);
	}

	public new static DeviceFileServerDownloadResponse Failed(string desc)
	{
		return new DeviceFileServerDownloadResponse(new CVBaseDeviceResponse(-1, desc), null);
	}
}
