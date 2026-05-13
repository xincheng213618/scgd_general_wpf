namespace MQTTMessageLib.FileServer;

public class DeviceFileServerUploadResponse : DeviceFileServerResponse
{
	public DeviceFileUpdownParam Data { get; set; }

	public DeviceFileServerUploadResponse(CVBaseDeviceResponse status, DeviceFileUpdownParam data)
		: base(status)
	{
		Data = data;
	}

	public DeviceFileServerUploadResponse(CVBaseDeviceResponse status)
		: base(status)
	{
		Data = null;
	}

	public static DeviceFileServerUploadResponse Success(DeviceFileUpdownParam data)
	{
		return new DeviceFileServerUploadResponse(CVBaseDeviceResponse.Success(), data);
	}

	public static DeviceFileServerUploadResponse Pending(DeviceFileUpdownParam data)
	{
		return new DeviceFileServerUploadResponse(CVBaseDeviceResponse.Pending(), data);
	}

	public new static DeviceFileServerUploadResponse Failed(string desc)
	{
		return new DeviceFileServerUploadResponse(CVBaseDeviceResponse.Failed(desc));
	}

	public new static DeviceFileServerUploadResponse Failed()
	{
		return new DeviceFileServerUploadResponse(CVBaseDeviceResponse.Failed());
	}
}
