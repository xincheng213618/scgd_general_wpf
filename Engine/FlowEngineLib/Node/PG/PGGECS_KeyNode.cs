using System.Collections.Generic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.PG;

[STNode("/06 PG")]
public class PGGECS_KeyNode : CVBaseServerNode
{
	private PGGECSKeyCmdType _PGCmd;

	private STNodeEditText<PGGECSKeyCmdType> m_ctrl_editText;

	[STNodeProperty("命令", "命令", true)]
	public PGGECSKeyCmdType PGCmd
	{
		get
		{
			return _PGCmd;
		}
		set
		{
			_PGCmd = value;
			m_ctrl_editText.Value = _PGCmd;
			OnPropertyChanged();
		}
	}

	public PGGECS_KeyNode()
		: base("PG.GECS.Key", "PG", "SVR.PG.Default", "DEV.PG.Default")
	{
		operatorCode = "ExecCmd";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<PGGECSKeyCmdType>), m_custom_item, "Command:", _PGCmd);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		List<PGParamFunction> list = new List<PGParamFunction>();
		PGParamFunction pGParamFunction = null;
		switch (_PGCmd)
		{
		case PGGECSKeyCmdType.KeyReset:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyReset"
			};
			break;
		case PGGECSKeyCmdType.KeyNext:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyNext"
			};
			break;
		case PGGECSKeyCmdType.KeyBack:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyBack"
			};
			break;
		case PGGECSKeyCmdType.KeyAuto:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyAuto"
			};
			break;
		case PGGECSKeyCmdType.KeyEnter:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyEnter"
			};
			break;
		case PGGECSKeyCmdType.KeyRepeat:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyRepeat"
			};
			break;
		case PGGECSKeyCmdType.KeyUp:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyUp"
			};
			break;
		case PGGECSKeyCmdType.KeyDown:
			pGParamFunction = new PGParamFunction
			{
				Name = "KeyDown"
			};
			break;
		}
		if (pGParamFunction != null)
		{
			list.Add(pGParamFunction);
		}
		return list;
	}
}
