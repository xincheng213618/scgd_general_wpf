using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

internal class CVXRCameraParam : CameraData
{
	public string XRType { get; set; }

	public CVTemplateParam XRTemplate { get; set; }

	public CVXRCameraParam(CVImageFlipMode flipMode, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName, string globalVariableName, string XRType, string XRTempName)
		: base(flipMode, enableFocus, focus, aperture, avgCount, gain, expTime, caliTempName, poiTempName, poiFilterTempName, poiReviseTempName, globalVariableName)
	{
		XRTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = XRTempName
		};
		this.XRType = XRType;
	}
}
