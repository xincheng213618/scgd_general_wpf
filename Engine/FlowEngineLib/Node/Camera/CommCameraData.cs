using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Camera;

public class CommCameraData
{
	public CVImageFlipMode FlipMode { get; set; }

	public CVTemplateParam AutoExpTimeTemplate { get; set; }

	public bool IsAutoExpWithND { get; set; }

	public bool IsAutoExpTime { get; set; }

	public bool IsHDR { get; set; }

	public CVTemplateParam CamParamTemplate { get; set; }

	public CVTemplateParam Calibration { get; set; }

	public CommCameraData(string camTempName, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, bool isHDR = false, CVImageFlipMode flipMode = CVImageFlipMode.None)
	{
		FlipMode = flipMode;
		IsAutoExpWithND = isWithND;
		IsAutoExpTime = isAutoExpTime;
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
		Calibration = new CVTemplateParam
		{
			ID = -1,
			Name = caliTempName
		};
	}
}

internal sealed class CommCameraPoiData : CommCameraData
{
	public POITemplateParam POIParam { get; set; }

	public CommCameraPoiData(string camTempName, bool isWithND, bool isAutoExpTime, string autoExpTempName, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName)
		: base(camTempName, isWithND, isAutoExpTime, autoExpTempName, caliTempName)
	{
		if (!string.IsNullOrEmpty(poiTempName))
			POIParam = new POITemplateParam(poiTempName, poiFilterTempName, poiReviseTempName);
	}
}
