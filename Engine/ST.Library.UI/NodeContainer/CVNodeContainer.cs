using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using ST.Library.UI.NodeEditor;

namespace ST.Library.UI.NodeContainer;

public class CVNodeContainer
{
	private CVNodeCollection _Nodes;

	protected static readonly Type m_type_node = typeof(STNode);

	private Dictionary<string, Type> m_dic_model_type = new Dictionary<string, Type>();

	[Browsable(false)]
	public CVNodeCollection Nodes => _Nodes;

	[Description("当节点被添加时候发生")]
	public event STNodeEditorEventHandler NodeAdded;

	[Description("当节点被移除时候发生")]
	public event STNodeEditorEventHandler NodeRemoved;

	public CVNodeContainer()
	{
		_Nodes = new CVNodeCollection(this);
	}

	public bool LoadAssemblyFromBase64(string base64Assembly)
	{
		byte[] rawAssembly = Convert.FromBase64String(base64Assembly);
		Assembly asm = Assembly.Load(rawAssembly);
		return LoadAssembly(asm);
	}

	public bool LoadAssembly(string strFile)
	{
		Assembly asm = Assembly.LoadFrom(strFile);
		return LoadAssembly(asm);
	}

	public bool LoadAssembly(Assembly asm)
	{
		bool result = false;
		if (asm == null)
		{
			return false;
		}
		Type[] types = asm.GetTypes();
		foreach (Type type in types)
		{
			if (!type.IsAbstract && (type == m_type_node || type.IsSubclassOf(m_type_node)))
			{
				string modelByType = GetModelByType(type);
				if (!m_dic_model_type.ContainsKey(modelByType))
				{
					m_dic_model_type.Add(modelByType, type);
					result = true;
				}
			}
		}
		return result;
	}

	private string GetModelByType(Type t)
	{
		return $"{t.Module.Name}|{t.FullName}";
	}

	public void LoadCanvas(string strFileName)
	{
		LoadCanvas(File.ReadAllBytes(strFileName));
	}

	public void LoadCanvas(byte[] byData)
	{
		using MemoryStream s = new MemoryStream(byData);
		LoadCanvas(s);
	}

	public void LoadCanvas(Stream s)
	{
		int num = 0;
		byte[] array = new byte[32];
		s.Read(array, 0, 5);
		if (!CheckHeader(array))
		{
			return;
		}
		Clear();
		using (GZipStream gZipStream = new GZipStream(s, CompressionMode.Decompress))
		{
			gZipStream.Read(array, 0, 16);
			float num2 = BitConverter.ToSingle(array, 0);
			float num3 = BitConverter.ToSingle(array, 4);
			float num4 = BitConverter.ToSingle(array, 8);
			int num5 = BitConverter.ToInt32(array, 12);
			Dictionary<long, STNodeOption> dictionary = new Dictionary<long, STNodeOption>();
			HashSet<STNodeOption> hashSet = new HashSet<STNodeOption>();
			byte[] array2 = null;
			for (int i = 0; i < num5; i++)
			{
				gZipStream.Read(array, 0, 4);
				num = BitConverter.ToInt32(array, 0);
				array2 = new byte[num];
				gZipStream.Read(array2, 0, array2.Length);
				STNode sTNode = null;
				try
				{
					sTNode = GetNodeFromData(array2);
				}
				catch (Exception ex)
				{
					throw new Exception("加载节点时发生错误可能数据已损坏\r\n" + ex.Message, ex);
				}
				if (sTNode == null)
				{
					continue;
				}
				try
				{
					_Nodes.Add(sTNode);
				}
				catch (Exception innerException)
				{
					throw new Exception("加载节点出错-" + sTNode.Title, innerException);
				}
				foreach (STNodeOption inputOption in sTNode.InputOptions)
				{
					if (hashSet.Add(inputOption))
					{
						dictionary.Add(dictionary.Count, inputOption);
					}
				}
				foreach (STNodeOption outputOption in sTNode.OutputOptions)
				{
					if (hashSet.Add(outputOption))
					{
						dictionary.Add(dictionary.Count, outputOption);
					}
				}
			}
			gZipStream.Read(array, 0, 4);
			num5 = BitConverter.ToInt32(array, 0);
			array2 = new byte[8];
			for (int j = 0; j < num5; j++)
			{
				gZipStream.Read(array2, 0, array2.Length);
				long num6 = BitConverter.ToInt64(array2, 0);
				long key = num6 >> 32;
				long key2 = (int)num6;
				if (dictionary.ContainsKey(key) && dictionary.ContainsKey(key2))
				{
					dictionary[key].ConnectOptionEx(dictionary[key2]);
				}
			}
		}
		foreach (STNode node in _Nodes)
		{
			node.OnEditorLoadCompleted();
		}
	}

	private void Clear()
	{
		_Nodes.Clear();
	}

	private bool CheckHeader(byte[] header)
	{
		if (BitConverter.ToInt32(header, 0) != STNodeConstant.NodeFlagInt)
		{
			throw new InvalidDataException("无法识别的文件类型");
		}
		if (header[4] != 1)
		{
			throw new InvalidDataException("无法识别的文件版本号");
		}
		return true;
	}

	private STNode GetNodeFromData(byte[] byData)
	{
		int num = 0;
		string @string = Encoding.UTF8.GetString(byData, num + 1, byData[num]);
		num += byData[num] + 1;
		string string2 = Encoding.UTF8.GetString(byData, num + 1, byData[num]);
		num += byData[num] + 1;
		int num2 = 0;
		Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
		while (num < byData.Length)
		{
			num2 = BitConverter.ToInt32(byData, num);
			num += 4;
			string string3 = Encoding.UTF8.GetString(byData, num, num2);
			num += num2;
			num2 = BitConverter.ToInt32(byData, num);
			num += 4;
			byte[] array = new byte[num2];
			Array.Copy(byData, num, array, 0, num2);
			num += num2;
			dictionary.Add(string3, array);
		}
		Type type = null;
		if (m_dic_model_type.ContainsKey(@string))
		{
			type = m_dic_model_type[@string];
		}
		if (type == null)
		{
			throw new TypeLoadException("无法找到类型 {" + @string.Split('|')[1] + "} 所在程序集 确保程序集 {" + @string.Split('|')[0] + "} 已被编辑器正确加载 可通过调用LoadAssembly()加载程序集");
		}
		STNode sTNode = (STNode)Activator.CreateInstance(type);
		sTNode.Create();
		sTNode.OnLoadNode(dictionary);
		return sTNode;
	}

	protected internal virtual void OnNodeAdded(STNodeEditorEventArgs e)
	{
		if (this.NodeAdded != null)
		{
			this.NodeAdded(this, e);
		}
	}

	protected internal virtual void OnNodeRemoved(STNodeEditorEventArgs e)
	{
		if (this.NodeRemoved != null)
		{
			this.NodeRemoved(this, e);
		}
	}
}
