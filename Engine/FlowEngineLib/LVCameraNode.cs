using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/02 相机")]
public class LVCameraNode : BaseCameraNode
{
	protected string _GlobalVariableName;

	public LVCameraNode()
		: base("L/BV相机", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new LVCameraData(_FlipMode, enableFocus: false, 0, 0f, _AvgCount, _Gain, new float[1] { _ExpTime }, _CaliTempName, _POITempName, _POIFilterTempName, _POIReviseTempName, _GlobalVariableName);
	}
}
