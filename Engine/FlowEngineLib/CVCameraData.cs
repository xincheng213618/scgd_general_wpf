using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

internal class CVCameraData : CameraData
{
	public int CV2LVChannel { get; set; } = -1;

	public CVCameraData(CVImageFlipMode flipMode, CV2LVChannelMode cv2LVChannel, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName, string poiTempName, string poiFilterTempName, string poiReviseTempName)
		: base(flipMode, enableFocus, focus, aperture, avgCount, gain, expTime, caliTempName, poiTempName, poiFilterTempName, poiReviseTempName, string.Empty)
	{
		CV2LVChannel = (int)cv2LVChannel;
	}
}
