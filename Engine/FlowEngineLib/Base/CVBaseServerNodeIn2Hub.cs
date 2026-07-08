using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public class CVBaseServerNodeIn2Hub : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVBaseServerNodeIn2Hub));

	protected STNodeOption m_in2_start;

	protected CVStartCFC[] masterInput;

	protected string m_in2_text;

	protected CVBaseServerNodeIn2Hub(string title, string nodeType, string nodeName, string deviceCode, int inNum = 2)
		: base(title, nodeType, nodeName, deviceCode)
	{
		m_in_text = "IN_1";
		m_in2_text = "IN_2";
		masterInput = new CVStartCFC[inNum];
		base.Height += 15;
		m_custom_item.Y += 15;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in2_start = base.InputOptions.Add(m_in2_text, typeof(CVStartCFC), bSingle: true);
		m_in2_start.Connected += m_in_op_Connected;
		m_in2_start.DataTransfer += m_in_start_DataTransfer;
	}

	protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		DoInputDataTransfer(sender as STNodeOption, e);
	}

	private void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected)
		{
			return;
		}
		if (HasData(e))
		{
			bool allInputsRunning = true;
			if (e.TargetOption.DataType == typeof(CVStartCFC))
			{
				CVStartCFC cVStartCFC = e.TargetOption.Data as CVStartCFC;
				cVStartCFC.NormalizeStopStatus();
				if (cVStartCFC.IsPaused)
				{
					DoTransferToServer(cVStartCFC, e);
					return;
				}
				if (ShouldEndFlowImmediately(cVStartCFC))
				{
					FinishFlow(cVStartCFC);
					clearData();
					clearInCFC();
					DoNodeEndedTransferData(cVStartCFC);
					return;
				}
				CVStartCFC data = new CVStartCFC(cVStartCFC);
				sender.Data = data;
				int num = 0;
				StatusTypeEnum statusType = StatusTypeEnum.Runing;
				for (int i = 0; i < base.InputOptionsCount; i++)
				{
					STNodeOption sTNodeOption = base.InputOptions[i];
					if (sTNodeOption.DataType == typeof(CVStartCFC))
					{
						CVStartCFC cVStartCFC2 = (CVStartCFC)sTNodeOption.Data;
						if (cVStartCFC2 != null)
						{
							if (!cVStartCFC.IsSameFlow(cVStartCFC2))
							{
								sTNodeOption.Data = null;
								masterInput[i] = null;
								continue;
							}
							cVStartCFC2.NormalizeStopStatus();
							if (!cVStartCFC2.IsRunning)
							{
								statusType = cVStartCFC2.FlowStatus;
								allInputsRunning = false;
							}
							masterInput[i] = cVStartCFC2;
							num++;
						}
					}
					else
					{
						logger.WarnFormat("TargetData Type is not flow common type => {0}", sTNodeOption.DataType.AssemblyQualifiedName);
					}
				}
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat("[{0}][{1}/{2}] DoServerTransfer => {3} [{4}/{5}]", ToShortString(), num, base.InputOptionsCount, cVStartCFC.ToShortString(), sender.Text, JsonConvert.SerializeObject(cVStartCFC.Data));
				}
				if (num == base.InputOptionsCount)
				{
					clearData();
					if (allInputsRunning)
					{
						DoTransferToServer(cVStartCFC, e);
					}
					else
					{
						cVStartCFC.SetStatusType(statusType);
						FinishFlow(cVStartCFC);
						DoNodeEndedTransferData(cVStartCFC);
					}
					clearInCFC();
				}
			}
			else
			{
				logger.WarnFormat("TargetData Type is not flow common type => {0}", e.TargetOption.DataType.AssemblyQualifiedName);
			}
		}
		else
		{
			clearData();
			clearInCFC();
			DoNodeEndedTransferData(null);
		}
	}

	private static bool ShouldEndFlowImmediately(CVStartCFC start)
	{
		return start.TryGetStopStatus(out _);
	}

	private static void FinishFlow(CVStartCFC start)
	{
		if (start.TryDoFinishing())
		{
			start.FireFinished();
		}
	}

	private void clearInCFC()
	{
		for (int i = 0; i < base.InputOptionsCount; i++)
		{
			masterInput[i] = null;
		}
	}

	private void clearData()
	{
		for (int i = 0; i < base.InputOptionsCount; i++)
		{
			STNodeOption sTNodeOption = base.InputOptions[i];
			if (sTNodeOption.DataType == typeof(CVStartCFC))
			{
				sTNodeOption.Data = null;
			}
		}
	}

	private void DoNodeEndedTransferData(CVStartCFC obj)
	{
		m_op_end.TransferData(obj);
	}
}
