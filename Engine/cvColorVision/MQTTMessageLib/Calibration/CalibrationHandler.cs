using System.Collections.Generic;

namespace MQTTMessageLib.Calibration;

public class CalibrationHandler
{
	public List<CalibrationItemCfg> ItemList { get; set; }

	public List<CalibrationItemCfg> CIEItemList { get; set; }

	public bool HasChannel
	{
		get
		{
			if (ItemList != null)
			{
				return ItemList.Count > 0;
			}
			return false;
		}
	}

	public bool HasCIE
	{
		get
		{
			if (CIEItemList != null)
			{
				return CIEItemList.Count == 1;
			}
			return false;
		}
	}

	public string GroupName { get; set; }

	public CalibrationHandler()
	{
		ItemList = new List<CalibrationItemCfg>();
		CIEItemList = new List<CalibrationItemCfg>();
	}
}
