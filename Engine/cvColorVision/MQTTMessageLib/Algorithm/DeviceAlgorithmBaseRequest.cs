using CVCommCore.CVImage;

namespace MQTTMessageLib.Algorithm;

public class DeviceAlgorithmBaseRequest<T> : DeviceCVBaseRequest<AlgorithmRequestType, T>, IDevAlgorithmRequest, IDeviceRequest
{
	public CVCIEFileInfo DataBuffer { get; set; }

	public bool IsPersistence { get; protected set; }

	public bool IsCIEImage { get; set; }

	public bool IsImageFilePreRequest { get; set; }

	public IDevAlgorithmRequest NextRequest { get; set; }

	public DeviceAlgorithmBaseRequest(string deviceCode, string serialNumber, AlgorithmRequestType request, T param)
		: this(deviceCode, serialNumber, -1, request, param, string.Empty)
	{
	}

	public DeviceAlgorithmBaseRequest(string deviceCode, string serialNumber, AlgorithmRequestType request, T param, string version)
		: this(deviceCode, serialNumber, -1, request, param, version)
	{
	}

	public DeviceAlgorithmBaseRequest(string deviceCode, string serialNumber, int zIndex, AlgorithmRequestType request, T param)
		: this(deviceCode, serialNumber, zIndex, request, param, string.Empty)
	{
	}

	public DeviceAlgorithmBaseRequest(string deviceCode, string serialNumber, int zIndex, AlgorithmRequestType request, T param, string version)
		: base(deviceCode, serialNumber, zIndex, request, version, param)
	{
		IsImageFilePreRequest = true;
		IsPersistence = true;
		IsCIEImage = false;
		NextRequest = null;
	}
}
