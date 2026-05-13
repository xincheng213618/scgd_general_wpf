namespace MQTTMessageLib.FileServer;

public class MQTTFileServerUploadResponse : MQTTCVBaseResponse<DeviceFileUpdownParam>
{
	public MQTTFileServerUploadResponse(MQTTCVRequestHeader request, DeviceFileServerUploadResponse respone)
		: base(request, new MQTTCVResponseStatus(respone.Code, respone.Desc), respone.Data)
	{
	}
}
