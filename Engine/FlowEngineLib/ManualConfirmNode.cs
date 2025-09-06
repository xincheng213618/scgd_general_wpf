using System.Windows.Forms;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/01 运算")]
public class ManualConfirmNode : CVCommonNodeHub
{
	private string _MessageText;

	[STNodeProperty("MessageText", "MessageText")]
	public string MessageText
	{
		get
		{
			return _MessageText;
		}
		set
		{
			_MessageText = value;
		}
	}

	public ManualConfirmNode()
		: base(bSingle: true, "手动确认")
	{
		_MessageText = "进行下一步!";
	}

	protected override void input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption option = sender as STNodeOption;
		int index = base.InputOptions.IndexOf(option);
		if (e.Status != ConnectionStatus.Connected)
		{
			base.OutputOptions[index].Data = null;
		}
		else
		{
			base.OutputOptions[index].Data = e.TargetOption.Data;
			if (base.OutputOptions[index].Data != null)
			{
				if (e.TargetOption.Data.GetType() == typeof(CVStartCFC))
				{
					if (((CVStartCFC)e.TargetOption.Data).FlowStatus == StatusTypeEnum.Runing)
					{
						MessageBox.Show(_MessageText, "手动确认", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					}
				}
				else
				{
					MessageBox.Show(_MessageText, "手动确认", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			}
		}
		base.OutputOptions[index].TransferData();
	}
}
