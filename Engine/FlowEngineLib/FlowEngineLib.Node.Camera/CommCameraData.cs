namespace FlowEngineLib.Node.Camera;

public class CommCameraData
{
	public CVTemplateParam AutoExpTimeTemplate { get; set; }

	public bool IsAutoExpTime { get; set; }

	public bool IsAutoFocus { get; set; }

	public bool IsHDR { get; set; }

	public POITemplateParam POIParam { get; set; }

	public CVTemplateParam AutoFocusTemplate { get; set; }

	public CVTemplateParam CamParamTemplate { get; set; }

	public CVTemplateParam Calibration { get; set; }

	public string GlobalVariableName { get; set; }

	public CommCameraData(string camTempName, bool isAutoExpTime, string autoExpTempName, bool isAutoFocus, string focusTempName, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName, string globalVariableName, bool isHDR = false)
	{
		IsAutoExpTime = isAutoExpTime;
		IsAutoFocus = isAutoFocus;
		IsHDR = isHDR;
		AutoExpTimeTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = autoExpTempName
		};
		CamParamTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = camTempName
		};
		AutoFocusTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = focusTempName
		};
		Calibration = new CVTemplateParam
		{
			ID = -1,
			Name = caliTempName
		};
		if (!string.IsNullOrEmpty(poiTempName))
		{
			POIParam = new POITemplateParam(poiTempName, poiFilterTempName, poiReviseTempName);
		}
		GlobalVariableName = globalVariableName;
	}
}
