using System.Collections.Generic;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.CVMath;

namespace MQTTMessageLib.Algorithm.Compliance;

public abstract class POIResultComplianceCIEList<T> : POIResultCIEList<T>, RuleAction where T : IDataIndex
{
	protected CVPOIMath<T> POIMath;

	public Dictionary<DataMetricsModel, POIResultDataComplianceCIE<T>> ResultData { get; set; }

	public Dictionary<int, POIResultDataComplianceCIE<T>> PointResultData { get; set; }

	public POIResultComplianceCIEList(List<POIResultCIE<T>> poi, CVPOIMath<T> math)
	{
		base.Data = poi;
		ResultData = new Dictionary<DataMetricsModel, POIResultDataComplianceCIE<T>>();
		PointResultData = new Dictionary<int, POIResultDataComplianceCIE<T>>();
		POIMath = math;
	}

	public bool GetResultData(ValidateRule rule)
	{
		bool flag = false;
		DataMetricsModel dataMetricsType = ValidateRuleResult.GetDataMetricsType(rule.RType);
		if (dataMetricsType == DataMetricsModel.SinglePoint)
		{
			return BuildResultDataFromSinglePoint(rule, dataMetricsType);
		}
		return BuildResultDataFromMath(rule, dataMetricsType);
	}

	private bool BuildResultDataFromMath(ValidateRule rule, DataMetricsModel dataType)
	{
		bool flag = false;
		if (ResultData.ContainsKey(dataType))
		{
			MetricsResultDataType cIEDataType = ValidateRuleResult.GetCIEDataType(rule.RType);
			POIResultDataComplianceCIE<T> pOIResultDataComplianceCIE = ResultData[dataType];
			float value = 0f;
			flag = GetResultData(pOIResultDataComplianceCIE.CIEResult, cIEDataType, 0, out value);
			if (flag)
			{
				pOIResultDataComplianceCIE.ComplianceResults.Add(new ValidateRuleResult
				{
					Rule = rule,
					Value = value
				});
			}
		}
		return flag;
	}

	private bool BuildResultDataFromSinglePoint(ValidateRule rule, DataMetricsModel dataType)
	{
		bool result = false;
		MetricsResultDataType cIEDataType = ValidateRuleResult.GetCIEDataType(rule.RType);
		foreach (POIResultCIE<T> datum in base.Data)
		{
			if (!datum.Point.Id.HasValue)
			{
				return false;
			}
			BuildResultRule(rule, datum, cIEDataType, dataType);
		}
		return result;
	}

	private void BuildResultRule(ValidateRule rule, POIResultCIEData<T> item, MetricsResultDataType cieType, DataMetricsModel dataType, int o_index = -1)
	{
		POIPoint point = item.Point;
		POIResultDataComplianceCIE<T> pOIResultDataComplianceCIE;
		if (PointResultData.ContainsKey(point.Id.Value))
		{
			pOIResultDataComplianceCIE = PointResultData[point.Id.Value];
		}
		else
		{
			string name = ((!string.IsNullOrEmpty(point.Name)) ? point.Name : $"{point.Id.Value}");
			pOIResultDataComplianceCIE = new POIResultDataComplianceCIE<T>
			{
				Name = name,
				CIEResult = item.Data,
				DataType = dataType,
				ComplianceResults = new List<ValidateRuleResult>()
			};
			PointResultData.Add(point.Id.Value, pOIResultDataComplianceCIE);
		}
		float value = 0f;
		if (GetResultData(pOIResultDataComplianceCIE.CIEResult, cieType, o_index, out value))
		{
			pOIResultDataComplianceCIE.ComplianceResults.Add(new ValidateRuleResult
			{
				Rule = rule,
				Value = value
			});
		}
	}

	protected abstract bool GetResultData(T cie, MetricsResultDataType cieType, int idx, out float value);

	public override void DoDataCalc()
	{
		foreach (KeyValuePair<DataMetricsModel, T> item in POIMath.DoCalc(base.Data))
		{
			POIResultDataComplianceCIE<T> pOIResultDataComplianceCIE = new POIResultDataComplianceCIE<T>
			{
				Name = item.Key.ToString(),
				CIEResult = item.Value,
				DataType = item.Key,
				ComplianceResults = new List<ValidateRuleResult>()
			};
			ResultData.Add(pOIResultDataComplianceCIE.DataType, pOIResultDataComplianceCIE);
		}
	}

	public override void DoRule()
	{
		foreach (POIResultDataComplianceCIE<T> value in ResultData.Values)
		{
			foreach (ValidateRuleResult complianceResult in value.ComplianceResults)
			{
				complianceResult.Calculated();
			}
		}
		foreach (POIResultDataComplianceCIE<T> value2 in PointResultData.Values)
		{
			foreach (ValidateRuleResult complianceResult2 in value2.ComplianceResults)
			{
				complianceResult2.Calculated();
			}
		}
	}

	public void BindResultRule(Dictionary<string, List<ValidateRule>> rules)
	{
		foreach (POIResultCIE<T> datum in base.Data)
		{
			string[] ruleNameKey = datum.GetRuleNameKey();
			for (int i = 0; i < ruleNameKey.Length; i++)
			{
				string text = ruleNameKey[i];
				if (string.IsNullOrEmpty(text) || !rules.ContainsKey(text))
				{
					continue;
				}
				foreach (ValidateRule item in rules[text])
				{
					MetricsResultDataType cIEDataType = ValidateRuleResult.GetCIEDataType(item.RType);
					BuildResultRule(item, datum, cIEDataType, DataMetricsModel.SinglePoint, i);
				}
			}
		}
	}
}
