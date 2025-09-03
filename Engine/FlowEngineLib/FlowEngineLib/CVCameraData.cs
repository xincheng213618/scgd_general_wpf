using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

internal class CVCameraData : CameraData
{
	public CVCameraData(CVImageFlipMode flipMode, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName, string globalVariableName)
		: base(flipMode, enableFocus, focus, aperture, avgCount, gain, expTime, caliTempName, poiTempName, poiFilterTempName, poiReviseTempName, globalVariableName)
	{
	}
}
