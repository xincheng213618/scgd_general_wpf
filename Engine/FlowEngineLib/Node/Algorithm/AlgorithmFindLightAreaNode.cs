using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmFindLightAreaNode : CVBaseServerNode
{
	private string _TempName;

	private int _TempId;

	private string _ImgFileName;

	private string _SavePOITempName;

	private int _BufferLen;

	private string _OIndex;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_oidx;

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			setTempName();
		}
	}

	[STNodeProperty("参数模板ID", "参数模板ID", true)]
	public int TempId
	{
		get
		{
			return _TempId;
		}
		set
		{
			_TempId = value;
			setTempName();
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
		}
	}

	[STNodeProperty("POI保存模板", "POI保存模板", true)]
	public string SavePOITempName
	{
		get
		{
			return _SavePOITempName;
		}
		set
		{
			_SavePOITempName = value;
		}
	}

	[STNodeProperty("缓存大小", "缓存大小", true)]
	public int BufferLen
	{
		get
		{
			return _BufferLen;
		}
		set
		{
			_BufferLen = value;
		}
	}

	[STNodeProperty("角点顺序", "角点顺序", true)]
	public string OIndex
	{
		get
		{
			return _OIndex;
		}
		set
		{
			setOIndex(value);
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	private void setOIndex(string value)
	{
		if (IsValidOIndex(value))
		{
			_OIndex = value;
			m_ctrl_oidx.Value = value;
		}
	}

	public AlgorithmFindLightAreaNode()
		: base("发光区定位", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "FindLightArea";
		_TempName = "";
		_SavePOITempName = "";
		_OIndex = "[0,1,2,3]";
		_TempId = -1;
		_BufferLen = 1024;
		base.Height += 25;
	}

	private bool IsValidOIndex(string value)
	{
		int[] array = JsonConvert.DeserializeObject<int[]>(value);
		if (array != null && array.Length == 4 && IsOIndexInRange(array))
		{
			return true;
		}
		return false;
	}

	private bool IsOIndexInRange(int[] oIndex)
	{
		for (int pGNodeProperty = 0; pGNodeProperty < oIndex.Length; pGNodeProperty++)
		{
			if (oIndex[pGNodeProperty] < 0 || oIndex[pGNodeProperty] > 3)
			{
				return false;
			}
		}
		return true;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
		m_custom_item.Y += 25;
		m_ctrl_oidx = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "角点顺序:", _OIndex);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		int[] oIndex = null;
		if (!string.IsNullOrEmpty(_OIndex))
		{
			oIndex = JsonConvert.DeserializeObject<int[]>(_OIndex);
		}
		FindLightAreaParam findLightAreaParam = new FindLightAreaParam(_TempId, _TempName, _ImgFileName, GetImageFileType(_ImgFileName), _SavePOITempName, oIndex);
		getPreStepParam(start, findLightAreaParam);
		findLightAreaParam.BufferLen = _BufferLen;
		findLightAreaParam.SMUData = GetSMUResult(start);
		return findLightAreaParam;
	}
}
