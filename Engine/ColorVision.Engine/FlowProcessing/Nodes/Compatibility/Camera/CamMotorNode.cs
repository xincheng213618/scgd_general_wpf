#nullable disable
using System.ComponentModel;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System.Drawing;
using System.Reflection;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("传感器", CategoryOrder = 700)]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.CamMotorNode")]
[FlowNodeDocumentation(
	"控制相机电机的焦距、光圈或执行自动对焦，跟随相机当前后端。",
	Usage = "配置设备代码和命令；本地相机须在物理配置中启用电机，自动对焦须选择行程匹配的模板。",
	Processing = "本地分支复用相机与电机会话，关闭且启用本地偏好时自动连接；服务分支沿用原请求。",
	Notes = "本地执行失败不补发服务请求。预览期间须先停止预览。停止流程会阻止后续对焦采样，当前 SDK 调用返回前保留句柄；本地分支不使用节点最大超时。自动对焦只返回位置和评价结果，不生成测量记录或替换当前流程图像。")]
public class CamMotorNode : CVBaseServerNode
{
	[Browsable(false), JsonIgnore]
	public override bool RequiresRemoteService => !LocalCameraMotorExecution.UsesLocalCamera(DeviceCode);

	protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request) => LocalCameraMotorExecution.Create(request);

	private CamMotorRunType _RunType;

	private bool _bAbs;

	private int _Pos;

	private float _Aperture;

	private string _AutoFocusTemp;

	private STNodeEditText<CamMotorRunType> m_ctrl_RunType;

	[STNodeProperty("命令", "命令", true)]
	public CamMotorRunType RunType
	{
		get
		{
			return _RunType;
		}
		set
		{
			SetRunType(value);
			OnPropertyChanged();
		}
	}

	[STNodeProperty("绝对位置", "绝对位置", true)]
	public bool IsAbs
	{
		get
		{
			return _bAbs;
		}
		set
		{
			_bAbs = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("焦距", "焦距", true)]
	public int Position
	{
		get
		{
			return _Pos;
		}
		set
		{
			_Pos = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("光圈", "光圈", true)]
	public float Aperture
	{
		get
		{
			return _Aperture;
		}
		set
		{
			_Aperture = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("自动对焦模板", "自动对焦模板", true)]
	[PropertyEditorType(typeof(AutoFocusTemplatePropertiesEditor))]
	public string AutoFocusTemp
	{
		get
		{
			return _AutoFocusTemp;
		}
		set
		{
			_AutoFocusTemp = value;
			OnPropertyChanged();
		}
	}

	public CamMotorNode()
		: base("电机[相机]", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "Move";
		_bAbs = true;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		Rectangle custom_item = m_custom_item;
		m_ctrl_RunType = CreateControl(typeof(STNodeEditText<CamMotorRunType>), custom_item, "命令:", _RunType);
	}

	private void SetRunType(CamMotorRunType value)
	{
		_RunType = value;
		m_ctrl_RunType.Value = value;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		object result = null;
		switch (_RunType)
		{
		case CamMotorRunType.焦距:
			operatorCode = "Move";
			result = new FocusPosData(_Pos, _bAbs);
			break;
		case CamMotorRunType.光圈:
			operatorCode = "MoveDiaphragm";
			result = new FocusApertureData(_Aperture);
			break;
		case CamMotorRunType.自动对焦:
			operatorCode = "AutoFocus";
			result = new AutoFocusData(_AutoFocusTemp);
			break;
		}
		return result;
	}

	private void Hide(string name, bool isHide)
	{
		PropertyInfo[] properties = GetType().GetProperties();
		for (int i = 0; i < properties.Length; i++)
		{
			object[] customAttributes = properties[i].GetCustomAttributes(inherit: true);
			foreach (object obj in customAttributes)
			{
				if (obj is STNodePropertyAttribute)
				{
					STNodePropertyAttribute sTNodePropertyAttribute = obj as STNodePropertyAttribute;
					if (sTNodePropertyAttribute.Name == name)
					{
						sTNodePropertyAttribute.IsHide = isHide;
					}
				}
			}
		}
	}
}
