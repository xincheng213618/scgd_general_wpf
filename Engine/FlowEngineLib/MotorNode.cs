using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class MotorNode : CVBaseServerNode
{
	private MotorRunType _RunType;

	private bool _bAbs;

	private int _Pos;

	private float _Aperture;

	private STNodeEditText<MotorRunType> m_ctrl_RunType;

	[STNodeProperty("命令", "命令", true)]
	public MotorRunType RunType
	{
		get
		{
			return _RunType;
		}
		set
		{
			_RunType = value;
			m_ctrl_RunType.Value = value;
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

	public MotorNode()
		: base("电机", "Motor", "SVR.Motor.Default", "DEV.Motor.Default")
	{
		operatorCode = "Move";
		_bAbs = true;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		Rectangle custom_item = m_custom_item;
		m_ctrl_RunType = CreateControl(typeof(STNodeEditText<MotorRunType>), custom_item, "命令:", _RunType);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		object result = null;
		switch (_RunType)
		{
		case MotorRunType.焦距:
			operatorCode = "Move";
			result = new FocusPosData(_Pos, _bAbs);
			break;
		case MotorRunType.光圈:
			operatorCode = "MoveDiaphragm";
			result = new FocusApertureData(_Aperture);
			break;
		}
		return result;
	}
}
