using CVCommCore;

namespace MQTTMessageLib.Algorithm.POI;

public class MQTTPOIGetDataResult : MQTTAlgorithmBaseResult, IPOIGetDataResult
{
	public int RecordCount { get; set; }

	public bool HasRecord { get; set; }

	public string POIImgFileName { get; set; }

	public string POITemplateName { get; set; }

	public int MasterResultType => (int)base.ResultType;

	public MQTTPOIGetDataResult(string _POIImgFileName, string _POITemplateName, AlgorithmResultType resultType, int recordCount, bool hasRecord, int masterId, string masterResultCode = null)
		: base(masterId, resultType, masterResultCode)
	{
		POIImgFileName = _POIImgFileName;
		POITemplateName = _POITemplateName;
		RecordCount = recordCount;
		HasRecord = hasRecord;
	}
}
