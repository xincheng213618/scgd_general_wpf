using System;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Logical;

[STNode("/01 运算")]
public class LogicalANDNode : STNodeInHub
{
	public static readonly ILog logger = LogManager.GetLogger(typeof(LogicalANDNode));

	private STNodeOption m_op_result;

	private int masterId;

	public LogicalANDNode()
		: base(bSingle: true, "逻辑与")
	{
		masterId = -1;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_op_result = base.OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
	}

	protected override void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data != null)
		{
			bool flag = true;
			if (e.TargetOption.DataType == typeof(CVStartCFC))
			{
				CVStartCFC cVStartCFC = e.TargetOption.Data as CVStartCFC;
				if (cVStartCFC.IsPaused)
				{
					DoResultOutTransferData(cVStartCFC);
				}
				else
				{
					CVStartCFC data = new CVStartCFC(cVStartCFC);
					sender.Data = data;
					int num = 0;
					StatusTypeEnum statusType = StatusTypeEnum.Runing;
					for (int i = 0; i < base.InputOptionsCount; i++)
					{
						STNodeOption sTNodeOption = base.InputOptions[i];
						if (sTNodeOption.DataType == typeof(CVStartCFC) && sTNodeOption.Data != null)
						{
							CVStartCFC cVStartCFC2 = (CVStartCFC)sTNodeOption.Data;
							if (cVStartCFC2.IsRunning)
							{
								flag = flag;
							}
							else
							{
								statusType = cVStartCFC2.FlowStatus;
								flag = !flag && false;
							}
							num++;
							if (cVStartCFC.IsRunning && i == 0 && cVStartCFC2.Data.ContainsKey("MasterId"))
							{
								masterId = Convert.ToInt32(cVStartCFC2.Data["MasterId"]);
							}
						}
					}
					logger.DebugFormat("{0}[{1}/{2}] - {3}/MasterId={4}", new object[5]
					{
						base.Title,
						num,
						base.InputOptionsCount - 1,
						JsonConvert.SerializeObject((object)cVStartCFC),
						masterId
					});
					if (num == base.InputOptionsCount - 1)
					{
						clearData();
						if (!flag)
						{
							cVStartCFC.SetStatusType(statusType);
						}
						DoResultOutTransferData(cVStartCFC);
					}
				}
			}
		}
		else
		{
			DoResultOutTransferData(null);
		}
		setDisplayData();
	}

	private void DoResultOutTransferData(CVStartCFC data)
	{
		if (data != null && data.IsRunning)
		{
			data.Data["MasterId"] = masterId;
			logger.DebugFormat("ResultOut => MasterId={0}", (object)masterId);
		}
		m_op_result.TransferData(data);
	}

	private void setDisplayData()
	{
		for (int i = 0; i < base.InputOptionsCount; i++)
		{
			STNodeOption sTNodeOption = base.InputOptions[i];
			if (sTNodeOption.DataType == typeof(CVStartCFC))
			{
				if (sTNodeOption.Data == null)
				{
					SetOptionText(sTNodeOption, "--");
					continue;
				}
				CVStartCFC cVStartCFC = sTNodeOption.Data as CVStartCFC;
				SetOptionText(sTNodeOption, cVStartCFC.GetActionType().ToString());
			}
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
}
