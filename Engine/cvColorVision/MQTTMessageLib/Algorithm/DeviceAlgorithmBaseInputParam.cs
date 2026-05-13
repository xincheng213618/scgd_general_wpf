using CVCommCore;
using CVCommCore.Core;
using MQTTMessageLib.Camera;
using MQTTMessageLib.SMU;

namespace MQTTMessageLib.Algorithm;

public class DeviceAlgorithmBaseInputParam : MasterResult, ICVLocalFileType
{
	public string ImgFileName { get; set; }

	public CVFileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public CVTemplateParam POITemplateParam { get; set; }

	public int POI_MasterId { get; set; }

	public CVResultType PreResultType => (CVResultType)base.MasterResultType;

	public string LocalFileName => ImgFileName;

	public SMUMasterResultData SMUData { get; set; }

	public string GetTemplateName()
	{
		if (POITemplateParam != null)
		{
			return $"{TemplateParam.Name},POI:{POITemplateParam.Name}";
		}
		return TemplateParam.Name;
	}

	public DeviceAlgorithmBaseInputParam()
	{
		ImgFileName = string.Empty;
		TemplateParam = new CVTemplateParam();
		FileType = CVFileExtType.None;
		base.MasterId = -1;
		POI_MasterId = -1;
	}

	public DeviceAlgorithmBaseInputParam(int templateId, string templateName)
		: this()
	{
		TemplateParam.ID = templateId;
		TemplateParam.Name = templateName;
	}
}
