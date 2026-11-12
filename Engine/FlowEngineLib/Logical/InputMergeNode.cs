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
	private readonly BranchJoinInputs joinInputs = new();

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
			OnPropertyChanged();
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
			OnPropertyChanged();
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
			if (e.TargetOption.DataType == typeof(CVStartCFC))
			{
				CVStartCFC cVStartCFC = e.TargetOption.Data as CVStartCFC;
				cVStartCFC.NormalizeStopStatus();
				if (cVStartCFC.IsPaused)
				{
					DoResultOutTransferData(e.TargetOption.Data);
				}
				else if (ShouldEndFlowImmediately(cVStartCFC))
				{
					FinishFlow(cVStartCFC);
					joinInputs.ClearRun(InputOptions, cVStartCFC);
					DoResultOutTransferData(cVStartCFC);
				}
				else
				{
					CVStartCFC[] inputs = joinInputs.Collect(InputOptions, sender, cVStartCFC);
					if (inputs != null && !cVStartCFC.HasFinished)
					{
						CompleteJoin(cVStartCFC, inputs);
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

	private void CompleteJoin(CVStartCFC action, CVStartCFC[] inputs)
	{
		StatusTypeEnum status = StatusTypeEnum.Runing;
		var values = new List<MergeAlgorithmPreStepParam>();
		for (int index = 0; index < inputs.Length; index++)
		{
			CVStartCFC input = inputs[index];
			if (input == null) continue;
			input.NormalizeStopStatus();
			if (!input.IsRunning) status = input.FlowStatus;
			var value = new MergeAlgorithmPreStepParam(index);
			getPreStepParam(input, value);
			values.Add(value);
		}
		if (status == StatusTypeEnum.Runing)
			setNextStepParam(action, values);
		else
		{
			action.SetStatusType(status);
			FinishFlow(action);
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
			return JsonConvert.SerializeObject(result);
		}
		return JsonConvert.SerializeObject(new Dictionary<string, List<T>> { { _OutPath, result } });
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
		logger.Debug(JsonConvert.SerializeObject(obj));
		m_op_result.TransferData(obj);
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
			logger.Warn("Failed to update InputMerge display data.", ex);
		}
	}

}
