using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;

namespace FlowEngineLib;

[STNode("相机", CategoryOrder = 200)]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.LVCameraNode")]
[FlowNodeDocumentation(
	"执行 L/BV 相机取图；复用当前相机会话，关闭且启用本地偏好时自动打开本地相机。",
	Usage = "设置平均次数、增益、曝光时间及校正模板，然后将输入、输出端口接入流程；POI 相关模板仅用于服务分支。",
	Processing = "本地分支保留节点曝光、增益、平均次数、校正和翻转，结果归入当前流程批次并交接内存帧；POI、过滤和修正在本地分支忽略。服务分支沿用原请求。",
	Notes = "相机已打开时修改本地偏好不切换后端。关闭时按设备配置自动打开本地测量会话，打开或取图失败不补发服务请求。最大超时仅对服务分支生效，本地分支等待操作完成或主动停止流程。本地分支按节点设置保存 CVRAW，先写数据库再保存图像；旧服务读取图像时须同步保存。CV、循环和通用相机节点不在转发范围内。")]
public class LVCameraNode : BaseCameraNode
{
	private bool saveFiles = true;
	private bool saveAsynchronously;

	[Category("本地相机")]
	[STNodeProperty("保存文件", "仅本地取图分支生效；关闭时只写 CVRAW 缓存。数据库始终先写入，后续色度参数沿用此设置。", true)]
	public bool SaveFiles { get => saveFiles; set { saveFiles = value; OnPropertyChanged(); } }

	[Category("本地相机")]
	[STNodeProperty("异步保存", "仅本地取图且保存文件时生效；开启后后台顺序写入图像和色度参数。旧服务需要读取图像时请关闭此项。", true)]
	public bool SaveAsynchronously { get => saveAsynchronously; set { saveAsynchronously = value; OnPropertyChanged(); } }

	internal CVFileSaveMode SaveMode => !SaveFiles ? CVFileSaveMode.MemoryOnly
		: SaveAsynchronously ? CVFileSaveMode.Asynchronous : CVFileSaveMode.Synchronous;

	protected override FlowLocalExecution? CreateLocalExecution(CVMQTTRequest request)
		=> LocalLvCameraExecution.Create(request, SaveMode);

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
