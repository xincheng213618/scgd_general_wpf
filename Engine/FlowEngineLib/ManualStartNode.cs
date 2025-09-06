using System;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

internal class ManualStartNode : BaseStartNode
{
	private string _sn;

	private ActionTypeEnum _Action;

	[STNodeProperty("SN", "SN")]
	public string SN
	{
		get
		{
			return _sn;
		}
		set
		{
			_sn = value;
		}
	}

	[STNodeProperty("OUT", "OUT")]
	public ActionTypeEnum Action
	{
		get
		{
			return _Action;
		}
		set
		{
			_Action = value;
			DoStartTransferData(new CVStartCFC(this, _Action, _sn));
		}
	}

	public ManualStartNode()
		: base("Start_Manual")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		_sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
	}
}
