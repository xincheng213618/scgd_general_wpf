using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/02 相机")]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.LVCameraNode")]
[FlowNodeDocumentation(
	"执行 L/BV 相机取图；复用当前相机会话，关闭且启用本地偏好时自动打开本地相机。",
	Usage = "设置平均次数、增益、曝光时间及校正模板，然后将输入、输出端口接入流程；POI 相关模板仅用于服务分支。",
	Processing = "本地分支保留节点曝光、增益、平均次数、校正和翻转，结果归入当前流程批次并交接内存帧；POI、过滤和修正在本地分支忽略。服务分支沿用原请求。",
	Notes = "相机已打开时修改本地偏好不切换后端。关闭时按设备配置自动打开本地测量会话，打开或取图失败不补发服务请求。本地分支遵循显示配置的图像保存设置；CV、循环和通用相机节点不在转发范围内。")]
public class LVCameraNode : BaseCameraNode
{
	protected override FlowLocalExecution? CreateLocalExecution(CVMQTTRequest request)
		=> LocalLvCameraExecution.Create(request);

	protected string? _GlobalVariableName;

	public LVCameraNode()
		: base("L/BV相机", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new CameraData(_FlipMode, enableFocus: false, 0, 0f, _AvgCount, _Gain, new float[1] { _ExpTime }, _CaliTempName, _POITempName, _POIFilterTempName, _POIReviseTempName, _GlobalVariableName);
	}
}
