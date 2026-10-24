using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

// Compound requests remain for the separate legacy loop and XR nodes.
internal class CameraPoiData : CameraData
{
	public POITemplateParam POIParam { get; set; }

	public CameraPoiData(CVImageFlipMode flipMode, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName)
		: base(flipMode, enableFocus, focus, aperture, avgCount, gain, expTime, caliTempName)
	{
		if (!string.IsNullOrEmpty(poiTempName))
			POIParam = new POITemplateParam(poiTempName, poiFilterTempName, poiReviseTempName);
	}
}
