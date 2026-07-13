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
			bool allInputsRunning = true;
			if (e.TargetOption.DataType == typeof(CVStartCFC))
			{
				CVStartCFC cVStartCFC = e.TargetOption.Data as CVStartCFC;
				cVStartCFC.NormalizeStopStatus();
				if (cVStartCFC.IsPaused)
				{
					DoResultOutTransferData(cVStartCFC);
				}
				else if (ShouldEndFlowImmediately(cVStartCFC))
				{
					EnsureStopData(cVStartCFC);
					FinishFlow(cVStartCFC);
					clearData();
					DoResultOutTransferData(cVStartCFC);
				}
				else
				{
					CVStartCFC data = new CVStartCFC(cVStartCFC);
					sender.Data = data;
					int num = 0;
					masterId = -1;
					StatusTypeEnum statusType = StatusTypeEnum.Runing;
					CVStartCFC stoppedInput = null;
					for (int i = 0; i < base.InputOptionsCount; i++)
					{
						STNodeOption sTNodeOption = base.InputOptions[i];
						if (sTNodeOption.DataType == typeof(CVStartCFC) && sTNodeOption.Data != null)
						{
							CVStartCFC cVStartCFC2 = (CVStartCFC)sTNodeOption.Data;
							if (!cVStartCFC.IsSameFlow(cVStartCFC2))
							{
								sTNodeOption.Data = null;
								continue;
							}
							cVStartCFC2.NormalizeStopStatus();
							if (!cVStartCFC2.IsRunning)
							{
								statusType = cVStartCFC2.FlowStatus;
								allInputsRunning = false;
								stoppedInput ??= cVStartCFC2;
							}
							num++;
							if (cVStartCFC.IsRunning && masterId < 0 && cVStartCFC2.Data.ContainsKey("MasterId"))
							{
								masterId = Convert.ToInt32(cVStartCFC2.Data["MasterId"]);
							}
						}
					}
					logger.DebugFormat("{0}[{1}/{2}] - {3}/MasterId={4}", base.Title, num, base.InputOptionsCount - 1, JsonConvert.SerializeObject(cVStartCFC), masterId);
					if (num == base.InputOptionsCount - 1)
					{
						clearData();
						if (!allInputsRunning)
						{
							CopyStopData(stoppedInput, cVStartCFC);
							cVStartCFC.SetStatusType(statusType);
							EnsureStopData(cVStartCFC);
							FinishFlow(cVStartCFC);
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

	private static bool ShouldEndFlowImmediately(CVStartCFC start)
	{
		return start.TryGetStopStatus(out _);
	}

	private void EnsureStopData(CVStartCFC start)
	{
		if (start == null || !ShouldEndFlowImmediately(start))
		{
			return;
		}
		string nodeName = base.Title;
		if (start.Data.TryGetValue("ErrorNodeName", out object errorNodeNameObj))
		{
			string errorNodeName = Convert.ToString(errorNodeNameObj);
			if (!string.IsNullOrWhiteSpace(errorNodeName))
			{
				nodeName = errorNodeName;
			}
			else
			{
				start.Data["ErrorNodeName"] = nodeName;
			}
		}
		else
		{
			start.Data["ErrorNodeName"] = nodeName;
		}
		if (!start.Data.ContainsKey(nodeName))
		{
			start.Data[nodeName] = start.FlowStatus.ToString();
		}
	}

	private static void CopyStopData(CVStartCFC source, CVStartCFC target)
	{
		if (source == null || target == null)
		{
			return;
		}
		foreach (var item in source.Data)
		{
			target.Data[item.Key] = item.Value;
		}
	}

	private static void FinishFlow(CVStartCFC start)
	{
		if (start.TryDoFinishing())
		{
			start.FireFinished();
		}
	}

	private void DoResultOutTransferData(CVStartCFC data)
	{
		if (data != null && data.IsRunning)
		{
			data.Data["MasterId"] = masterId;
			logger.DebugFormat("ResultOut => MasterId={0}", masterId);
		}
		m_op_result.TransferData(data);
	}

	private void setDisplayData()
	{
		try
		{
			for (int i = 0; i < base.InputOptionsCount; i++)
			{
				STNodeOption sTNodeOption = base.InputOptions[i];
				if (sTNodeOption == null || sTNodeOption.DataType != typeof(CVStartCFC))
				{
					continue;
				}

				object data = sTNodeOption.Data;
				if (data is not CVStartCFC cVStartCFC)
				{
					SetOptionText(sTNodeOption, "--");
					if (data != null)
					{
						logger.WarnFormat("{0} input display ignored invalid data type. Option={1}, DataType={2}, ValueType={3}",
							base.Title,
							sTNodeOption.Text,
							sTNodeOption.DataType?.FullName ?? "<null>",
							data.GetType().FullName);
					}
					continue;
				}

				SetOptionText(sTNodeOption, cVStartCFC.GetActionType().ToString());
			}
		}
		catch (Exception ex)
		{
			logger.Warn("Failed to update LogicalAND display data.", ex);
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
