using System.Drawing;
using System.Reflection;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/07 传感器")]
public class CamMotorNode : CVBaseServerNode
{
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
		}
	}

	[STNodeProperty("自动对焦模板", "自动对焦模板", true)]
	public string AutoFocusTemp
	{
		get
		{
			return _AutoFocusTemp;
		}
		set
		{
			_AutoFocusTemp = value;
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
		switch (_RunType)
		{
		case CamMotorRunType.焦距:
			Hide("焦距", isHide: false);
			Hide("绝对位置", isHide: false);
			Hide("光圈", isHide: true);
			Hide("自动对焦模板", isHide: true);
			break;
		case CamMotorRunType.光圈:
			Hide("焦距", isHide: true);
			Hide("绝对位置", isHide: true);
			Hide("光圈", isHide: false);
			Hide("自动对焦模板", isHide: true);
			break;
		case CamMotorRunType.自动对焦:
			Hide("焦距", isHide: true);
			Hide("绝对位置", isHide: true);
			Hide("光圈", isHide: true);
			Hide("自动对焦模板", isHide: false);
			break;
		}
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
