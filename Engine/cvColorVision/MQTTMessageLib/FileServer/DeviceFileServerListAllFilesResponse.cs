namespace MQTTMessageLib.FileServer;

public class DeviceFileServerListAllFilesResponse : DeviceFileServerResponse
{
	public DeviceListAllFilesParam Data { get; set; }

	public DeviceFileServerListAllFilesResponse(int code, string desc, DeviceListAllFilesParam data)
		: base(code, desc)
	{
		Data = data;
	}

	public DeviceFileServerListAllFilesResponse(CVBaseDeviceResponse status, DeviceListAllFilesParam data)
		: base(status)
	{
		Data = data;
	}

	public static DeviceFileServerListAllFilesResponse Success(DeviceListAllFilesParam data)
	{
		return new DeviceFileServerListAllFilesResponse(CVBaseDeviceResponse.Success(), data);
	}
}
