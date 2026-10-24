#nullable disable
using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

internal class CVCameraData : CameraData
{
	public int CV2LVChannel { get; set; } = -1;

	public CVCameraData(CVImageFlipMode flipMode, CV2LVChannelMode cv2LVChannel, bool enableFocus, int focus, float aperture, int avgCount, float gain, float[] expTime, string caliTempName)
		: base(flipMode, enableFocus, focus, aperture, avgCount, gain, expTime, caliTempName)
	{
		CV2LVChannel = (int)cv2LVChannel;
	}
}
