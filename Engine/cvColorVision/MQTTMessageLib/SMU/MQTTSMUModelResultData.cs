namespace MQTTMessageLib.SMU;

public class MQTTSMUModelResultData
{
	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public MQTTParamScanModel ScanRequestParam { get; set; }

	public SMUResultData ResultData { get; set; }

	public MQTTSMUModelResultData(DeviceSMUModelMeasureResponse response)
	{
		MasterId = response.MasterId;
		MasterResultType = response.DeviceResultType;
		ScanRequestParam = new MQTTParamScanModel(response.Data.ScanRequestParam);
		ResultData = response.Data.ResultData;
	}
}
