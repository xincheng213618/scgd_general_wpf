using System.Collections.Generic;
using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class DeviceParamPOIOutput : ParamDicBase
{
	private bool _XYZIsEnable;

	private string _XYZFileName;

	private bool _XIsEnable;

	private string _XFileName;

	private bool _YIsEnable;

	private string _YFileName;

	private bool _ZIsEnable;

	private string _ZFileName;

	private bool _xIsEnable;

	private string _xFileName;

	private bool _yIsEnable;

	private string _yFileName;

	private bool _uIsEnable;

	private string _uFileName;

	private bool _vIsEnable;

	private string _vFileName;

	private bool _WaveIsEnable;

	private string _WaveFileName;

	private bool _CCTIsEnable;

	private string _CCTFileName;

	private int _Width;

	private int _Height;

	private string _MaskFileName;

	public bool XYZIsEnable
	{
		get
		{
			return GetValue(_XYZIsEnable, "XYZIsEnable");
		}
		set
		{
			SetProperty(ref _XYZIsEnable, value, "XYZIsEnable");
		}
	}

	public string XYZFileName
	{
		get
		{
			return GetValue(_XYZFileName, "XYZFileName");
		}
		set
		{
			SetProperty(ref _XYZFileName, value, "XYZFileName");
		}
	}

	public bool XIsEnable
	{
		get
		{
			return GetValue(_XIsEnable, "XIsEnable");
		}
		set
		{
			SetProperty(ref _XIsEnable, value, "XIsEnable");
		}
	}

	public string XFileName
	{
		get
		{
			return GetValue(_XFileName, "XFileName");
		}
		set
		{
			SetProperty(ref _XFileName, value, "XFileName");
		}
	}

	public bool YIsEnable
	{
		get
		{
			return GetValue(_YIsEnable, "YIsEnable");
		}
		set
		{
			SetProperty(ref _YIsEnable, value, "YIsEnable");
		}
	}

	public string YFileName
	{
		get
		{
			return GetValue(_YFileName, "YFileName");
		}
		set
		{
			SetProperty(ref _YFileName, value, "YFileName");
		}
	}

	public bool ZIsEnable
	{
		get
		{
			return GetValue(_ZIsEnable, "ZIsEnable");
		}
		set
		{
			SetProperty(ref _ZIsEnable, value, "ZIsEnable");
		}
	}

	public string ZFileName
	{
		get
		{
			return GetValue(_ZFileName, "ZFileName");
		}
		set
		{
			SetProperty(ref _ZFileName, value, "ZFileName");
		}
	}

	public bool xIsEnable
	{
		get
		{
			return GetValue(_xIsEnable, "xIsEnable");
		}
		set
		{
			SetProperty(ref _xIsEnable, value, "xIsEnable");
		}
	}

	public string xFileName
	{
		get
		{
			return GetValue(_xFileName, "xFileName");
		}
		set
		{
			SetProperty(ref _xFileName, value, "xFileName");
		}
	}

	public bool yIsEnable
	{
		get
		{
			return GetValue(_yIsEnable, "yIsEnable");
		}
		set
		{
			SetProperty(ref _yIsEnable, value, "yIsEnable");
		}
	}

	public string yFileName
	{
		get
		{
			return GetValue(_yFileName, "yFileName");
		}
		set
		{
			SetProperty(ref _yFileName, value, "yFileName");
		}
	}

	public bool uIsEnable
	{
		get
		{
			return GetValue(_uIsEnable, "uIsEnable");
		}
		set
		{
			SetProperty(ref _uIsEnable, value, "uIsEnable");
		}
	}

	public string uFileName
	{
		get
		{
			return GetValue(_uFileName, "uFileName");
		}
		set
		{
			SetProperty(ref _uFileName, value, "uFileName");
		}
	}

	public bool vIsEnable
	{
		get
		{
			return GetValue(_vIsEnable, "vIsEnable");
		}
		set
		{
			SetProperty(ref _vIsEnable, value, "vIsEnable");
		}
	}

	public string vFileName
	{
		get
		{
			return GetValue(_vFileName, "vFileName");
		}
		set
		{
			SetProperty(ref _vFileName, value, "vFileName");
		}
	}

	public bool WaveIsEnable
	{
		get
		{
			return GetValue(_WaveIsEnable, "WaveIsEnable");
		}
		set
		{
			SetProperty(ref _WaveIsEnable, value, "WaveIsEnable");
		}
	}

	public string WaveFileName
	{
		get
		{
			return GetValue(_WaveFileName, "WaveFileName");
		}
		set
		{
			SetProperty(ref _WaveFileName, value, "WaveFileName");
		}
	}

	public bool CCTIsEnable
	{
		get
		{
			return GetValue(_CCTIsEnable, "CCTIsEnable");
		}
		set
		{
			SetProperty(ref _CCTIsEnable, value, "CCTIsEnable");
		}
	}

	public string CCTFileName
	{
		get
		{
			return GetValue(_CCTFileName, "CCTFileName");
		}
		set
		{
			SetProperty(ref _CCTFileName, value, "CCTFileName");
		}
	}

	public int Width
	{
		get
		{
			return GetValue(_Width, "Width");
		}
		set
		{
			SetProperty(ref _Width, value, "Width");
		}
	}

	public int Height
	{
		get
		{
			return GetValue(_Height, "Height");
		}
		set
		{
			SetProperty(ref _Height, value, "Height");
		}
	}

	public string MaskFileName
	{
		get
		{
			return GetValue(_MaskFileName, "MaskFileName");
		}
		set
		{
			SetProperty(ref _MaskFileName, value, "MaskFileName");
		}
	}

	public List<POIOutputItem> ToOutputItem()
	{
		List<POIOutputItem> list = new List<POIOutputItem>();
		if (XYZIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_XYZ;
			pOIOutputItem.SuffixFileName = XYZFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 3;
			pOIOutputItem.ExpTime = new float[3];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item = pOIOutputItem;
			list.Add(item);
		}
		if (XIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_X;
			pOIOutputItem.SuffixFileName = XFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item2 = pOIOutputItem;
			list.Add(item2);
		}
		if (YIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_Y;
			pOIOutputItem.SuffixFileName = YFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item3 = pOIOutputItem;
			list.Add(item3);
		}
		if (ZIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_Z;
			pOIOutputItem.SuffixFileName = ZFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item4 = pOIOutputItem;
			list.Add(item4);
		}
		if (xIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_x;
			pOIOutputItem.SuffixFileName = xFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item5 = pOIOutputItem;
			list.Add(item5);
		}
		if (yIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_y;
			pOIOutputItem.SuffixFileName = yFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item6 = pOIOutputItem;
			list.Add(item6);
		}
		if (uIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_u;
			pOIOutputItem.SuffixFileName = uFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item7 = pOIOutputItem;
			list.Add(item7);
		}
		if (vIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CIE_v;
			pOIOutputItem.SuffixFileName = vFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item8 = pOIOutputItem;
			list.Add(item8);
		}
		if (WaveIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.Wave;
			pOIOutputItem.SuffixFileName = WaveFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item9 = pOIOutputItem;
			list.Add(item9);
		}
		if (CCTIsEnable)
		{
			POIOutputItem pOIOutputItem = new POIOutputItem();
			pOIOutputItem.DataType = MetricsResultDataType.CCT;
			pOIOutputItem.SuffixFileName = CCTFileName;
			pOIOutputItem.Cols = Width;
			pOIOutputItem.Rows = Height;
			pOIOutputItem.Channels = 1;
			pOIOutputItem.ExpTime = new float[1];
			pOIOutputItem.MaskFileUrl = MaskFileName;
			POIOutputItem item10 = pOIOutputItem;
			list.Add(item10);
		}
		return list;
	}
}
