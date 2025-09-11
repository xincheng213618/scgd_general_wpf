using System.Collections.Generic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.PG;

[STNode("/07 传感器")]
public class PGNode : CVBaseServerNode
{
	private PGCommCmdType _PGCmd;

	private STNodeEditText<PGCommCmdType> m_ctrl_editText;

	[STNodeProperty("命令", "命令", true)]
	public PGCommCmdType PGCmd
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

	[STNodeProperty("指定画面", "指定画面", true)]
	public int IndexFrame { get; set; }

	public PGNode()
		: base("PG", "PG", "SVR.PG.Default", "DEV.PG.Default")
	{
		operatorCode = "SetParam";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = new STNodeEditText<PGCommCmdType>();
		m_ctrl_editText.Text = "命令 ";
		m_ctrl_editText.DisplayRectangle = m_custom_item;
		m_ctrl_editText.Value = _PGCmd;
		base.Controls.Add(m_ctrl_editText);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		List<PGParamFunction> list = new List<PGParamFunction>();
		PGParamFunction pGParamFunction = null;
		switch (_PGCmd)
		{
		case PGCommCmdType.开始:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_StartPG"
			};
			break;
		case PGCommCmdType.停止:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_StopPG"
			};
			break;
		case PGCommCmdType.重置:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_ReSetPG"
			};
			break;
		case PGCommCmdType.上:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_SwitchUpPG"
			};
			break;
		case PGCommCmdType.下:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_SwitchDownPG"
			};
			break;
		case PGCommCmdType.指定:
			pGParamFunction = new PGParamFunction
			{
				Name = "CM_SwitchFramePG",
				Params = new Dictionary<string, object> { { "index", IndexFrame } }
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
