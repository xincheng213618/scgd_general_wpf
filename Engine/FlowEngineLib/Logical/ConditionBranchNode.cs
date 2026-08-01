using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Drawing;
using System.Globalization;

namespace FlowEngineLib.Logical;

public enum FlowConditionSource
{
	FlowStatus,
	DataField
}

public enum FlowConditionOperator
{
	Exists,
	NotExists,
	Equal,
	NotEqual,
	GreaterThan,
	GreaterThanOrEqual,
	LessThan,
	LessThanOrEqual,
	Contains,
	NotContains
}

[STNode("/01 运算", "根据流程状态或 Data 字段自动选择 TRUE/FALSE 路径")]
public sealed class ConditionBranchNode : CVCommonNode
{
	private FlowConditionSource conditionSource;
	private StatusTypeEnum expectedStatus;
	private string dataKey = string.Empty;
	private FlowConditionOperator conditionOperator;
	private string compareValue = string.Empty;
	private STNodeOption outputTrue = STNodeOption.Empty;
	private STNodeOption outputFalse = STNodeOption.Empty;
	private STNodeOption outputError = STNodeOption.Empty;

	[STNodeProperty("条件来源", "按流程状态或 Data 字段进行判断", true)]
	public FlowConditionSource ConditionSource
	{
		get => conditionSource;
		set
		{
			conditionSource = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("期望状态", "条件来源为 FlowStatus 时使用", true)]
	public StatusTypeEnum ExpectedStatus
	{
		get => expectedStatus;
		set
		{
			expectedStatus = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("数据键", "条件来源为 DataField 时读取的 Data 字段名称", true)]
	public string DataKey
	{
		get => dataKey;
		set
		{
			dataKey = value ?? string.Empty;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("比较方式", "Data 字段的比较方式", true)]
	public FlowConditionOperator ConditionOperator
	{
		get => conditionOperator;
		set
		{
			conditionOperator = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("比较值", "Equal、大小比较和 Contains 使用的目标值", true)]
	public string CompareValue
	{
		get => compareValue;
		set
		{
			compareValue = value ?? string.Empty;
			OnPropertyChanged();
		}
	}

	public ConditionBranchNode()
		: base("条件分支", "ConditionBranch", "CB1", "DEV01")
	{
		conditionSource = FlowConditionSource.FlowStatus;
		expectedStatus = StatusTypeEnum.Runing;
		conditionOperator = FlowConditionOperator.Equal;
		AutoSize = false;
		Width = StandardNodeWidth;
		Height = 100;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		TitleColor = Color.FromArgb(200, Color.DarkCyan);
		STNodeOption input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		outputTrue = OutputOptions.Add("OUT_TRUE", typeof(CVStartCFC), bSingle: false);
		outputFalse = OutputOptions.Add("OUT_FALSE", typeof(CVStartCFC), bSingle: false);
		outputError = OutputOptions.Add("OUT_ERROR", typeof(CVStartCFC), bSingle: false);
		SetOptionTextColor(outputTrue, Color.LightGreen);
		SetOptionTextColor(outputFalse, Color.LightGray);
		SetOptionTextColor(outputError, Color.OrangeRed);
		input.DataTransfer += Input_DataTransfer;
	}

	private void Input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected || e.TargetOption.Data is not CVStartCFC start)
		{
			return;
		}

		if (!TryEvaluate(start, out bool conditionResult, out string errorMessage))
		{
			start.Data ??= new System.Collections.Generic.Dictionary<string, object>();
			start.Data["ConditionErrorNodeName"] = Title;
			start.Data["ConditionErrorNodeId"] = NodeID;
			start.Data["ConditionError"] = errorMessage;
			outputError.TransferData(start);
			return;
		}

		if (conditionResult)
		{
			outputTrue.TransferData(start);
		}
		else
		{
			outputFalse.TransferData(start);
		}
	}

	private bool TryEvaluate(CVStartCFC start, out bool result, out string errorMessage)
	{
		result = false;
		errorMessage = string.Empty;
		if (ConditionSource == FlowConditionSource.FlowStatus)
		{
			start.NormalizeStopStatus();
			result = start.FlowStatus == ExpectedStatus;
			return true;
		}

		if (string.IsNullOrWhiteSpace(DataKey))
		{
			errorMessage = "条件分支的数据键不能为空。";
			return false;
		}

		object fieldValue = null;
		bool exists = start.Data != null && start.Data.TryGetValue(DataKey, out fieldValue);
		if (ConditionOperator == FlowConditionOperator.Exists)
		{
			result = exists;
			return true;
		}
		if (ConditionOperator == FlowConditionOperator.NotExists)
		{
			result = !exists;
			return true;
		}
		if (!exists)
		{
			result = false;
			return true;
		}

		string actualValue = Convert.ToString(fieldValue, CultureInfo.InvariantCulture) ?? string.Empty;
		switch (ConditionOperator)
		{
		case FlowConditionOperator.Equal:
			result = ValuesEqual(actualValue, CompareValue);
			return true;
		case FlowConditionOperator.NotEqual:
			result = !ValuesEqual(actualValue, CompareValue);
			return true;
		case FlowConditionOperator.Contains:
			result = actualValue.Contains(CompareValue, StringComparison.OrdinalIgnoreCase);
			return true;
		case FlowConditionOperator.NotContains:
			result = !actualValue.Contains(CompareValue, StringComparison.OrdinalIgnoreCase);
			return true;
		case FlowConditionOperator.GreaterThan:
		case FlowConditionOperator.GreaterThanOrEqual:
		case FlowConditionOperator.LessThan:
		case FlowConditionOperator.LessThanOrEqual:
			return TryEvaluateNumericComparison(actualValue, out result, out errorMessage);
		default:
			errorMessage = $"不支持的比较方式：{ConditionOperator}";
			return false;
		}
	}

	private bool TryEvaluateNumericComparison(string actualValue, out bool result, out string errorMessage)
	{
		result = false;
		errorMessage = string.Empty;
		if (!TryParseDecimal(actualValue, out decimal actualNumber) || !TryParseDecimal(CompareValue, out decimal expectedNumber))
		{
			errorMessage = $"字段 {DataKey} 的值“{actualValue}”或比较值“{CompareValue}”不是有效数字。";
			return false;
		}

		result = ConditionOperator switch
		{
			FlowConditionOperator.GreaterThan => actualNumber > expectedNumber,
			FlowConditionOperator.GreaterThanOrEqual => actualNumber >= expectedNumber,
			FlowConditionOperator.LessThan => actualNumber < expectedNumber,
			FlowConditionOperator.LessThanOrEqual => actualNumber <= expectedNumber,
			_ => false
		};
		return true;
	}

	private static bool ValuesEqual(string actualValue, string expectedValue)
	{
		if (TryParseDecimal(actualValue, out decimal actualNumber) && TryParseDecimal(expectedValue, out decimal expectedNumber))
		{
			return actualNumber == expectedNumber;
		}
		return string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryParseDecimal(string value, out decimal number)
	{
		return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
	}
}
