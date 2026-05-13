using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public abstract class POIResultComplianceContrastCIEList<T> : RuleAction
{
	protected CVContrastMath<ComplianceResultOnlyDataCIE<T>> ContrastMath;

	public List<POIResultDataComplianceCIE<T>> ResultData { get; set; }

	public OperationType OpType { get; set; }

	public List<ComplianceResultOnlyDataCIE<T>> Data1 { get; set; }

	public List<ComplianceResultOnlyDataCIE<T>> Data2 { get; set; }

	public List<ValidateRuleResult> RuleResult { get; set; }

	protected POIResultComplianceContrastCIEList(List<ComplianceResultOnlyDataCIE<T>> data1, List<ComplianceResultOnlyDataCIE<T>> data2, OperationType operationType)
	{
		Data1 = data1;
		Data2 = data2;
		OpType = operationType;
		ResultData = new List<POIResultDataComplianceCIE<T>>();
	}

	public virtual void DoDataCalc()
	{
		foreach (ComplianceResultOnlyDataCIE<T> item2 in ContrastMath.DoCalc(Data1, Data2, OpType))
		{
			POIResultDataComplianceCIE<T> item = new POIResultDataComplianceCIE<T>
			{
				Name = item2.Name,
				CIEResult = item2.CIEResult,
				DataType = item2.DataType,
				ComplianceResults = new List<ValidateRuleResult>()
			};
			ResultData.Add(item);
		}
	}

	public virtual void DoRule()
	{
		foreach (POIResultDataComplianceCIE<T> resultDatum in ResultData)
		{
			foreach (ValidateRuleResult complianceResult in resultDatum.ComplianceResults)
			{
				complianceResult.Calculated();
			}
		}
	}

	public virtual bool GetResultData(ValidateRule rule)
	{
		bool flag = false;
		float value = 0f;
		DataMetricsModel dataMetricsType = ValidateRuleResult.GetDataMetricsType(rule.RType);
		foreach (POIResultDataComplianceCIE<T> resultDatum in ResultData)
		{
			MetricsResultDataType cIEDataType = ValidateRuleResult.GetCIEDataType(rule.RType);
			if (dataMetricsType == resultDatum.DataType)
			{
				flag = GetResultData(resultDatum.CIEResult, cIEDataType, out value);
				if (flag)
				{
					resultDatum.ComplianceResults.Add(new ValidateRuleResult
					{
						Rule = rule,
						Value = value
					});
				}
			}
		}
		return flag;
	}

	protected abstract bool GetResultData(T cie, MetricsResultDataType cieType, out float value);
}
