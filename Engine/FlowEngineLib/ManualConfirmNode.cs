using ColorVision.Themes.Controls;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Windows;

namespace FlowEngineLib;

[STNode("/01 运算")]
public class ManualConfirmNode : CVCommonNodeHub
{
	public static readonly ILog logger = LogManager.GetLogger(typeof(ManualConfirmNode));

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
			OnPropertyChanged();
		}
	}

	public ManualConfirmNode()
		: base(bSingle: true, "手动确认")
	{
		_MessageText = "Next Step!";
	}

	private void ShowConfirmation()
	{
		Application application = Application.Current
			?? throw new InvalidOperationException("Manual confirmation requires a WPF application.");
		application.Dispatcher.Invoke(() =>
		{
			MessageBox1.Show(
				application.GetActiveWindow(),
				_MessageText,
				Properties.Resources.手动确认,
				MessageBoxButton.OK,
				MessageBoxImage.Warning);
		});
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
				if (e.TargetOption.Data is CVStartCFC start)
				{
					if (start.FlowStatus == StatusTypeEnum.Runing)
					{
						ShowConfirmation();
						if (logger.IsInfoEnabled)
						{
							logger.Info("Manual Next Step");
						}
					}
				}
				else
				{
					ShowConfirmation();
					if (logger.IsInfoEnabled)
					{
						logger.Info("Manual Next Step");
					}
				}
			}
		}
		base.OutputOptions[index].TransferData();
	}
}
