namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumGetDataResponse : MQTTCVBaseResponse<MQTTSpectrumResult>
{
	public MQTTSpectrumGetDataResponse(MQTTCVRequestHeader request, DeviceSpectrumMeasureResponse response)
		: base(request, new MQTTCVResponseStatus(response), new MQTTSpectrumResult(response))
	{
	}
}
