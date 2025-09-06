using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmBlackMuraNode : CVBaseServerNode
{
	private string _TempName;

	private int _TempId;

	private string _ImgFileName;

	private string _OIndex;

	private string _SavePOITempName;

	private STNodeEditText<string> m_ctrl_temp;

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

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmBlackMuraNode()
		: base("BlackMura算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "BlackMura.Caculate";
		_TempName = "";
		_TempId = -1;
		_OIndex = "[0,1,2,3]";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		int[] oIndex = null;
		if (!string.IsNullOrEmpty(_OIndex))
		{
			oIndex = JsonConvert.DeserializeObject<int[]>(_OIndex);
		}
		BlackMuraParam blackMuraParam = new BlackMuraParam(_TempId, _TempName, _ImgFileName, GetImageFileType(_ImgFileName), _SavePOITempName, oIndex);
		getPreStepParam(start, blackMuraParam);
		return blackMuraParam;
	}

	private void setOIndex(string value)
	{
		if (IsValidOIndex(value))
		{
			_OIndex = value;
		}
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
		for (int i = 0; i < oIndex.Length; i++)
		{
			if (oIndex[i] < 0 || oIndex[i] > 3)
			{
				return false;
			}
		}
		return true;
	}
}
