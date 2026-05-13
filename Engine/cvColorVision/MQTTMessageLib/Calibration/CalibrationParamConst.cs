using System.Collections.Generic;
using MQTTMessageLib.Tool;

namespace MQTTMessageLib.Calibration;

public class CalibrationParamConst
{
	public static CalibrationParamItem[] CalibrationParamItems = new CalibrationParamItem[8]
	{
		InitItem(CalibrationType.DarkNoise),
		InitItem(CalibrationType.DSNU),
		InitItem(CalibrationType.DefectPoint),
		InitItem(CalibrationType.Uniformity),
		InitItem(CalibrationType.Distortion),
		InitItem(CalibrationType.ColorShift),
		InitItem(CalibrationType.ColorDiff),
		InitItem(CalibrationType.LineArity)
	};

	public static CalibrationParamItem[] ColorCalibrationParamItems = new CalibrationParamItem[4]
	{
		InitItem(CalibrationType.Luminance),
		InitItem(CalibrationType.LumOneColor),
		InitItem(CalibrationType.LumFourColor),
		InitItem(CalibrationType.LumMultiColor)
	};

	private static CalibrationParamItem InitItem(CalibrationType caliType)
	{
		string text = caliType.ToString();
		return new CalibrationParamItem
		{
			CaliType = caliType,
			IsSelectedItemName = text + "IsSelected",
			ItemName = text,
			ItemId = text + "Id"
		};
	}

	public static CalibrationItemCfg BuildCalibrationItem(CalibrationParamItem item, Dictionary<string, KeyValuePair<string, string>> valuePairs)
	{
		return new CalibrationItemCfg
		{
			CalibrationType = item.CaliType,
			Selected = SysDicTool.GetValue(valuePairs, first: true, storage: false, item.IsSelectedItemName),
			Title = SysDicTool.GetValue(valuePairs, first: true, string.Empty, item.ItemName),
			FileName = SysDicTool.GetValue(valuePairs, first: false, string.Empty, item.ItemId),
			ResId = SysDicTool.GetValue(valuePairs, first: true, -1, item.ItemId)
		};
	}

	public static void AddCali(CalibrationItemCfg calibration, List<CalibrationItemCfg> list)
	{
		if (calibration.IsValid && calibration.Selected)
		{
			list.Add(calibration);
		}
	}

	public static void InitParam(Dictionary<string, KeyValuePair<string, string>> valuePairs, List<CalibrationItemCfg> itemList)
	{
		for (int i = 0; i < CalibrationParamItems.Length; i++)
		{
			AddCali(BuildCalibrationItem(CalibrationParamItems[i], valuePairs), itemList);
		}
	}

	public static void InitColorParam(Dictionary<string, KeyValuePair<string, string>> valuePairs, List<CalibrationItemCfg> itemList)
	{
		for (int i = 0; i < ColorCalibrationParamItems.Length; i++)
		{
			AddCali(BuildCalibrationItem(ColorCalibrationParamItems[i], valuePairs), itemList);
		}
	}

	public static void InitGroup(Dictionary<string, KeyValuePair<string, string>> valuePairs, CalibrationHandler cali)
	{
		cali.GroupName = SysDicTool.GetValue(valuePairs, first: true, string.Empty, "CalibrationMode");
	}
}
