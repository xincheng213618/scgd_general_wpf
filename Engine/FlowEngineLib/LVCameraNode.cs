using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;

namespace FlowEngineLib;

[STNode("/02 相机")]
[FlowNodeDocumentation(
	"执行 L/BV 相机取图；当前设备已打开本地相机时使用本地会话，否则通过相机服务执行。",
	Usage = "设置平均次数、增益、曝光时间及校正模板，然后将输入、输出端口接入流程；POI 相关模板仅用于服务分支。",
	Processing = "本地分支保留节点曝光、增益、平均次数、校正和翻转，结果归入当前流程批次并交接内存帧；POI、过滤和修正在本地分支忽略。服务分支沿用原请求。",
	Notes = "仅勾选本地偏好不会改变执行后端，须先打开本地测量会话。本地分支遵循显示配置的图像保存设置；CV、循环和通用相机节点不在转发范围内。")]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.CaliTempName), typeof(FlowEngineLib.PropertyEditor.FlowCalibrationTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.POITempName), typeof(FlowEngineLib.PropertyEditor.FlowPoiTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.POIFilterTempName), typeof(FlowEngineLib.PropertyEditor.FlowPoiFilterTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute(nameof(BaseCameraNode.POIReviseTempName), typeof(FlowEngineLib.PropertyEditor.FlowPoiReviseTemplateEditor))]
public class LVCameraNode : BaseCameraNode
{
	// The host supplies native camera work without an Engine dependency in FlowEngineLib.
	public static Func<CVMQTTRequest, FlowLocalExecution> LocalExecutionFactory { get; set; }

	protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request)
		=> LocalExecutionFactory?.Invoke(request);

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
