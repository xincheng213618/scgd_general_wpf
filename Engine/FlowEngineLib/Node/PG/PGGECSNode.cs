using System.Collections.Generic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.PG;

[STNode("/06 PG")]
public class PGGECSNode : CVBaseServerNode
{
	private PGGECSCommCmdType _PGCmd;

	private STNodeEditText<PGGECSCommCmdType> m_ctrl_editText;

	[STNodeProperty("命令", "命令", true)]
	public PGGECSCommCmdType PGCmd
	{
		get
		{
			return _PGCmd;
		}
		set
		{
			_PGCmd = value;
			m_ctrl_editText.Value = _PGCmd;
		}
	}

	[STNodeProperty("参数", "参数", true)]
	public string CmdParam { get; set; }

	public PGGECSNode()
		: base("PG.GECS", "PG", "SVR.PG.Default", "DEV.PG.Default")
	{
		operatorCode = "ExecCmd";
		CmdParam = string.Empty;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<PGGECSCommCmdType>), m_custom_item, "Command:", _PGCmd);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		List<PGParamFunction> list = new List<PGParamFunction>();
		PGParamFunction pGParamFunction = null;
		switch (_PGCmd)
		{
		case PGGECSCommCmdType.上电:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_StartPG"
			};
			break;
		case PGGECSCommCmdType.下电:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_StopPG"
			};
			break;
		case PGGECSCommCmdType.上:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_SwitchUpPG"
			};
			break;
		case PGGECSCommCmdType.下:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_SwitchDownPG"
			};
			break;
		case PGGECSCommCmdType.指定:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_SwitchFramePG",
				Params = new Dictionary<string, object> { { "index", CmdParam } }
			};
			break;
		case PGGECSCommCmdType.电压电流:
			pGParamFunction = new PGParamFunction
			{
				Name = "POWER.MEASURE"
			};
			break;
		case PGGECSCommCmdType.EFUSE_DieId:
			pGParamFunction = new PGParamFunction
			{
				Name = "EFUSE.DieId",
				Params = new Dictionary<string, object> { { "DieId", CmdParam } }
			};
			break;
		case PGGECSCommCmdType.OSC_TRIM:
			pGParamFunction = new PGParamFunction
			{
				Name = "OSC.TRIM"
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
