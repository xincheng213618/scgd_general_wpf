using System;
using System.IO;
using Newtonsoft.Json;

namespace CVCommCore.CVAlgorithm;

public class POIOutputItem
{
	public MetricsResultDataType DataType { get; set; }

	public string SuffixFileName { get; set; }

	public string FileUrl { get; set; }

	public string MaskFileUrl { get; set; }

	[JsonIgnore]
	public float[] Data { get; set; }

	[JsonIgnore]
	public byte[] LvData { get; set; }

	public int Cols { get; set; }

	public int Rows { get; set; }

	public int Channels { get; set; } = 1;

	public float[] ExpTime { get; set; }

	public bool IsCVCIEFileType { get; private set; }

	public void AddData(int idx, float value)
	{
		if (Data != null)
		{
			Data[idx] = value;
		}
		else if (LvData != null)
		{
			int num = 4;
			int dstOffset = idx * num;
			Buffer.BlockCopy(BitConverter.GetBytes(value), 0, LvData, dstOffset, num);
		}
	}

	public void AddData(float[] x, float[] y, float[] z)
	{
		int num = Cols * Rows;
		int num2 = num * 4;
		if ((x != null && x.Length < num) || (y != null && y.Length < num) || (z != null && z.Length < num))
		{
			throw new ArgumentException("Input arrays too small");
		}
		if (LvData != null)
		{
			if (LvData.Length < num2 * 3)
			{
				throw new InvalidOperationException("LvData too small");
			}
			Buffer.BlockCopy(x, 0, LvData, 0, num2);
			Buffer.BlockCopy(y, 0, LvData, num2, num2);
			Buffer.BlockCopy(z, 0, LvData, num2 * 2, num2);
		}
		else if (Data != null)
		{
			byte[] array = new byte[num2 * 3];
			Buffer.BlockCopy(x, 0, array, 0, num2);
			Buffer.BlockCopy(y, 0, array, num2, num2);
			Buffer.BlockCopy(z, 0, array, num2 * 2, num2);
			Buffer.BlockCopy(array, 0, Data, 0, num2 * 3);
		}
	}

	public void BuildFileName(string dataFilePath, string preFileName)
	{
		string path = $"{preFileName}{SuffixFileName}";
		FileUrl = Path.Combine(dataFilePath, path);
		string text = Path.GetExtension(FileUrl).ToLower();
		if (text.IndexOf("cvcie") >= 0 || text.IndexOf("cvraw") >= 0)
		{
			IsCVCIEFileType = true;
		}
		else
		{
			IsCVCIEFileType = false;
		}
	}

	public void InitDataBuffer(POIHeaderInfo POIHeader, int len)
	{
		if (IsCVCIEFileType)
		{
			LvData = new byte[len * 4 * Channels];
		}
		else
		{
			Data = new float[len * Channels];
		}
		if (Cols <= 0 || Rows <= 0 || !IsTotalLen(len))
		{
			if (POIHeader == null || !POIHeader.IsTotalLen(len))
			{
				Rows = 1;
				Cols = len;
			}
			else
			{
				Cols = POIHeader.Cols;
				Rows = POIHeader.Rows;
			}
		}
	}

	public bool IsTotalLen(int len)
	{
		return Cols * Rows == len;
	}
}
