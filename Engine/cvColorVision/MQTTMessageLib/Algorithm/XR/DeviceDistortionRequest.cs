namespace MQTTMessageLib.Algorithm.XR;

public class DeviceDistortionRequest : DeviceAlgorithmBaseRequest<DistortionGetDataParam>
{
	public DeviceDistortionRequest(string deviceName, string serialNumber, int zIndex, DistortionGetDataParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.Distortion_GetData, param)
	{
	}
}
