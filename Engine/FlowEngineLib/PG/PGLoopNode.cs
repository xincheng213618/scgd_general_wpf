using System.Collections.Generic;
using FlowEngineLib.Base;
using FlowEngineLib.Control;
using FlowEngineLib.Node.PG;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.PG;

public class PGLoopNode : CVBaseLoopServerNode<PGNodeProperty>
{
	[STNodeProperty("参数", "Get or set the PG params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<PGNodeProperty, FormPGParam>))]
	public List<PGNodeProperty> Params
	{
		get
		{
			return _params;
		}
		set
		{
			_params = value;
			updateUI();
		}
	}

	public PGLoopNode()
		: base("PG.For", "PG", "SVR.PG.Default", "DEV.PG.Default")
	{
		operatorCode = "SetParam";
	}

	private PGParamFunction BuildCmd(PGNodeProperty param)
	{
		PGParamFunction result = null;
		switch (param.Cmd)
		{
		case PGCommCmdType.开始:
			result = new PGParamFunction
			{
				Name = "CM_StartPG"
			};
			break;
		case PGCommCmdType.停止:
			result = new PGParamFunction
			{
				Name = "CM_StopPG"
			};
			break;
		case PGCommCmdType.重置:
			result = new PGParamFunction
			{
				Name = "CM_ReSetPG"
			};
			break;
		case PGCommCmdType.上:
			result = new PGParamFunction
			{
				Name = "CM_SwitchUpPG"
			};
			break;
		case PGCommCmdType.下:
			result = new PGParamFunction
			{
				Name = "CM_SwitchDownPG"
			};
			break;
		case PGCommCmdType.指定:
			result = new PGParamFunction
			{
				Name = "CM_SwitchFramePG",
				Params = new Dictionary<string, object> { 
				{
					"index",
					param.Data.IndexFrame
				} }
			};
			break;
		}
		return result;
	}

	protected override object getBaseEventData(CVStartCFC start, PGNodeProperty property)
	{
		List<PGParamFunction> list = new List<PGParamFunction>();
		PGParamFunction item = BuildCmd(property);
		list.Add(item);
		return list;
	}
}
