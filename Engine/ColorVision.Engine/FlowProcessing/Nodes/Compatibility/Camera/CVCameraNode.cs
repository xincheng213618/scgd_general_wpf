#nullable disable
using System.ComponentModel;
using ColorVision.Engine.PropertyEditor;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using Newtonsoft.Json;
using System.Linq;

namespace FlowEngineLib;

[STNode("相机", CategoryOrder = 200, NodeOrder = 20)]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.CVCameraNode")]
[FlowNodeDocumentation(
	"执行 CV 相机取图；复用当前相机会话，关闭且启用本地偏好时自动打开本地相机。",
	Usage = "配置 R/G/B 独立曝光、增益、平均次数和校正模板；本地分支要求 CV 三通道相机，CV2LVChannel 为 None。",
	Processing = "本地分支保留三路曝光、校正和翻转，按节点设置保存 CVRAW，结果归入当前流程批次并交接内存帧；POI 测量由下游独立节点执行。",
	Notes = "服务已打开时仍沿用服务；修改本地偏好不切换已有连接。打开或取图失败不补发服务请求。本地分支不使用节点最大超时，主动停止后释放晚到帧，不继续下游。循环节点不在转发范围内。")]
public class CVCameraNode : CVBaseServerNode
{
	private bool saveFiles = true;

	[Browsable(false), JsonIgnore]
	public bool ShowLocalCameraSettings => ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
		.Any(camera => camera.Code == DeviceCode && camera.CameraBackend.OpensLocally) == true;

	[Category("本地相机")]
	[PropertyVisibility(nameof(ShowLocalCameraSettings))]
	[STNodeProperty("保存文件", "仅本地取图分支生效；启用时保存 CVRAW，写入完成后继续；关闭时只写缓存。数据库始终先写入，后续色度参数沿用此设置。", true)]
	public bool SaveFiles { get => saveFiles; set { saveFiles = value; OnPropertyChanged(); } }

	internal CVFileSaveMode SaveMode => SaveFiles ? CVFileSaveMode.Synchronous : CVFileSaveMode.MemoryOnly;

	protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request)
		=> LocalCameraFlowExecution.Create(request, SaveMode);

	protected int _AvgCount;

	protected float _Gain;

	protected float _TempR;

	protected float _TempG;

	protected float _TempB;

	protected CV2LVChannelMode _CV2LVChannel;

	protected float _Aperture;

	protected bool _EnableFocus;

	protected int _Focus;

	protected string _CalibTempName;

	protected CVImageFlipMode _FlipMode;

	private STNodeEditText<string> m_ctrl_expTime;

	[STNodeProperty("平均次数", "平均次数", true)]
	public int AvgCount
	{
		get
		{
			return _AvgCount;
		}
		set
		{
			_AvgCount = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("增益", "增益", true)]
	public float Gain
	{
		get
		{
			return _Gain;
		}
		set
		{
			_Gain = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("R曝光", "RExpDesc", true)]
	public float TempR
	{
		get
		{
			return _TempR;
		}
		set
		{
			_TempR = value;
			setExposureDisplay();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("G曝光", "GExpDesc", true)]
	public float TempG
	{
		get
		{
			return _TempG;
		}
		set
		{
			_TempG = value;
			setExposureDisplay();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("B曝光", "BExpDesc", true)]
	public float TempB
	{
		get
		{
			return _TempB;
		}
		set
		{
			_TempB = value;
			setExposureDisplay();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("CV2LVChannel", "CV2LVChannel", true)]
	public CV2LVChannelMode CV2LVChannel
	{
		get
		{
			return _CV2LVChannel;
		}
		set
		{
			_CV2LVChannel = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("校正模板", "校正模板", true)]
	[PropertyEditorType(typeof(CalibrationTemplatePropertiesEditor))]
	public string CalibTempName
	{
		get
		{
			return _CalibTempName;
		}
		set
		{
			_CalibTempName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像翻转", "图像翻转", true)]
	public CVImageFlipMode FlipMode
	{
		get
		{
			return _FlipMode;
		}
		set
		{
			_FlipMode = value;
			OnPropertyChanged();
		}
	}

	public CVCameraNode()
		: base("CV相机", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData";
		_FlipMode = CVImageFlipMode.None;
		_CV2LVChannel = CV2LVChannelMode.None;
		_MaxTime = 20000;
		_CalibTempName = "";
		_TempR = 100f;
		_TempG = 100f;
		_TempB = 100f;
		_Gain = 10f;
		_AvgCount = 1;
		_Aperture = 0f;
		_EnableFocus = false;
		_Focus = 0;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		initCtrl();
	}

	public override void ApplyCompactNodeDisplay()
	{
		ShowControls = true;
		SetAutoSize(true);
	}

	private string GetExposureDisplay()
	{
		return $"{_TempR}/{_TempG}/{_TempB}";
	}

	private void setExposureDisplay()
	{
		m_ctrl_expTime.Value = GetExposureDisplay();
	}

	protected override int GetMaxDelay()
	{
		return base.GetMaxDelay() + (int)(_TempR + _TempG + _TempB);
	}

	private void initCtrl()
	{
		m_ctrl_expTime = CreateStringControl(m_custom_item, "", GetExposureDisplay());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		float[] expTime = new float[3] { _TempR, _TempG, _TempB };
		return new CVCameraData(_FlipMode, _CV2LVChannel, _EnableFocus, _Focus, _Aperture, _AvgCount, _Gain, expTime, _CalibTempName);
	}
}
