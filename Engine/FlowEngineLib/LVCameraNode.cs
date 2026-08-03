using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/02 相机")]
[FlowNodeDocumentation(
	"通过相机服务执行 L/BV 相机取图，可同时传递校正、图像翻转和 POI 模板。",
	Usage = "设置平均次数、增益和曝光时间；需要时选择校正模板及 POI 相关模板，然后将输入、输出端口接入流程。",
	Processing = "节点将取图参数封装为 GetData 请求并发送给配置的相机服务；采集、校正、翻转和 POI 均由相机服务端实现，本节点不执行本地 opencv_helper 处理链。",
	Notes = "该节点依赖 SVR.Camera.Default/DEV.Camera.Default 服务配置。需要直接使用本机相机和本地校正缓存时，应选择“本地相机取图”节点。")]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.CaliTempName), typeof(FlowEngineLib.PropertyEditor.FlowCalibrationTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.POITempName), typeof(FlowEngineLib.PropertyEditor.FlowPoiTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.POIFilterTempName), typeof(FlowEngineLib.PropertyEditor.FlowPoiFilterTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.POIReviseTempName), typeof(FlowEngineLib.PropertyEditor.FlowPoiReviseTemplateEditor))]
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
