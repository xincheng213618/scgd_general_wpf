namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumEQEGetDataResponse : MQTTCVBaseResponse<MQTTSpectrumEQEResult>
{
	public MQTTSpectrumEQEGetDataResponse(MQTTCVRequestHeader request, DeviceSpectrumEQEMeasureResponse response)
		: base(request, new MQTTCVResponseStatus(response), new MQTTSpectrumEQEResult(response))
	{
	}
}
