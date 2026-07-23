using System.Collections.Generic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.PG;

[STNode("/06 PG")]
public class PGGECS_DemuraNode : CVBaseServerNode
{
	private PGGECSDemuraCmdType _PGCmd;

	private STNodeEditText<PGGECSDemuraCmdType> m_ctrl_editText;

	[STNodeProperty("命令", "命令", true)]
	public PGGECSDemuraCmdType PGCmd
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

	public PGGECS_DemuraNode()
		: base("PG.GECS.Demura", "PG", "SVR.PG.Default", "DEV.PG.Default")
	{
		operatorCode = "ExecCmd";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<PGGECSDemuraCmdType>), m_custom_item, "Command:", _PGCmd);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		List<PGParamFunction> list = new List<PGParamFunction>();
		PGParamFunction pGParamFunction = null;
		switch (_PGCmd)
		{
		case PGGECSDemuraCmdType.EraseStart:
			pGParamFunction = new PGParamFunction
			{
				Name = "DEMURA.ERASE.START"
			};
			break;
		case PGGECSDemuraCmdType.WriteStart:
			pGParamFunction = new PGParamFunction
			{
				Name = "DEMURA.WRITE.START"
			};
			break;
		case PGGECSDemuraCmdType.On:
			pGParamFunction = new PGParamFunction
			{
				Name = "DEMURA.ON"
			};
			break;
		case PGGECSDemuraCmdType.Off:
			pGParamFunction = new PGParamFunction
			{
				Name = "DEMURA.OFF"
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
