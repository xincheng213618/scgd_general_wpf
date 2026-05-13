namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumGetParamResponse : MQTTCVBaseResponse<SpectrumSysParam>
{
	public MQTTSpectrumGetParamResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status, SpectrumSysParam data)
		: base(request, status, data)
	{
	}
}
