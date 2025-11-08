using System;
using System.Collections.Generic;
using System.Linq;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Logical;

public class InputMergeNode : STNodeInHub
{
	public static readonly ILog logger = LogManager.GetLogger(typeof(InputMergeNode));

	private MergePathType _PathType;

	private string _OutPath;

	private STNodeOption m_op_result;

	[STNodeProperty("合并值", "合并值", true)]
	public MergePathType PathType
	{
		get
		{
			return _PathType;
		}
		set
		{
			_PathType = value;
		}
	}

	[STNodeProperty("输出路径", "输出路径", true)]
	public string OutPath
	{
		get
		{
			return _OutPath;
		}
		set
		{
			_OutPath = value;
		}
	}

	public InputMergeNode()
		: base(bSingle: true)
	{
		base.Title = "组合";
		_PathType = MergePathType.ID;
		_OutPath = "";
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
					DoResultOutTransferData(e.TargetOption.Data);
				}
				else
				{
					CVStartCFC data = new CVStartCFC(cVStartCFC);
					sender.Data = data;
					int num = 0;
					List<MergeAlgorithmPreStepParam> list = new List<MergeAlgorithmPreStepParam>();
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
							MergeAlgorithmPreStepParam mergeAlgorithmPreStepParam = new MergeAlgorithmPreStepParam(i);
							getPreStepParam(cVStartCFC2, mergeAlgorithmPreStepParam);
							list.Add(mergeAlgorithmPreStepParam);
							num++;
						}
					}
					logger.DebugFormat("{0}/{1} - {2}", (object)num, (object)(base.InputOptionsCount - 1), (object)JsonConvert.SerializeObject((object)cVStartCFC));
					if (num == base.InputOptionsCount - 1)
					{
						clearData();
						if (flag)
						{
							setNextStepParam(cVStartCFC, list);
						}
						else
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

	private void setNextStepParam(CVStartCFC start, List<MergeAlgorithmPreStepParam> master)
	{
		string value = string.Empty;
		List<MergeAlgorithmPreStepParam> master2 = master.OrderBy((MergeAlgorithmPreStepParam n) => n.order).ToList();
		switch (PathType)
		{
		case MergePathType.ID:
			value = GetMasterValueById(master2);
			break;
		case MergePathType.Value:
			value = GetMasterValueByValue(master2);
			break;
		}
		start.Data["MasterValue"] = value;
		start.Data["MasterResultType"] = 41;
		start.Data["MasterId"] = -1;
	}

	private string GetMasterValueById(List<MergeAlgorithmPreStepParam> master)
	{
		List<int> list = new List<int>();
		for (int i = 0; i < master.Count; i++)
		{
			list.Add(master[i].MasterId);
		}
		return BuildPathMasterValue(list);
	}

	private string BuildPathMasterValue<T>(List<T> result)
	{
		if (string.IsNullOrEmpty(_OutPath))
		{
			return JsonConvert.SerializeObject((object)result);
		}
		return JsonConvert.SerializeObject((object)new Dictionary<string, List<T>> { { _OutPath, result } });
	}

	private string GetMasterValueByValue(List<MergeAlgorithmPreStepParam> master)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < master.Count; i++)
		{
			list.Add(master[i].MasterValue);
		}
		return BuildPathMasterValue(list);
	}

	protected void getPreStepParam(CVStartCFC start, AlgorithmPreStepParam param)
	{
		int value = -1;
		int masterResultType = -1;
		string key = "MasterResultType";
		string value2 = string.Empty;
		if (start.GetDataValueString(key, ref value2))
		{
			masterResultType = Convert.ToInt32(value2);
		}
		key = "MasterId";
		start.GetDataValueInt(key, ref value);
		key = "MasterValue";
		if (start.GetDataValueString(key, ref value2))
		{
			param.MasterValue = value2;
		}
		param.MasterId = value;
		param.MasterResultType = masterResultType;
	}

	private void DoResultOutTransferData(object obj)
	{
		logger.Debug((object)JsonConvert.SerializeObject(obj));
		m_op_result.TransferData(obj);
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
