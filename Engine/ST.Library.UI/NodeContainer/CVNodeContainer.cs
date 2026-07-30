using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Linq;
using ST.Library.UI.NodeEditor;

namespace ST.Library.UI.NodeContainer;

public class CVNodeContainer : IDisposable
{
	private CVNodeCollection _Nodes;

	private bool _disposed;

	protected static readonly Type m_type_node = typeof(STNode);

	[Browsable(false)]
	public float CanvasOffsetX { get; private set; } = 10f;

	[Browsable(false)]
	public float CanvasOffsetY { get; private set; } = 10f;

	[Browsable(false)]
	public float CanvasScale { get; private set; } = 1f;

	[Browsable(false)]
	public CVNodeCollection Nodes => _Nodes;

	[Description("当节点被添加时候发生")]
	public event STNodeEditorEventHandler NodeAdded;

	[Description("当节点被移除时候发生")]
	public event STNodeEditorEventHandler NodeRemoved;

	public CVNodeContainer()
	{
		_Nodes = new CVNodeCollection(this);
		STNodeTypeRegistry.Initialize();
	}

	public bool LoadAssemblyFromBase64(string base64Assembly)
	{
		byte[] rawAssembly = Convert.FromBase64String(base64Assembly);
		Assembly asm = Assembly.Load(rawAssembly);
		return LoadAssembly(asm);
	}

	public bool LoadAssembly(string strFile)
	{
		return STNodeTypeRegistry.LoadAssembly(strFile);
	}

	public bool LoadAssembly(Assembly asm)
	{
		return STNodeTypeRegistry.LoadAssembly(asm);
	}

	public int LoadAssembly()
	{
		return STNodeTypeRegistry.LoadAssemblies(AppDomain.CurrentDomain.GetAssemblies());
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
		ObjectDisposedException.ThrowIf(_disposed, this);
		STNodeCanvasReader.Document document = STNodeCanvasReader.Read(s);
		document.ConnectDetachedNodes();

		// Commit only after the complete stream and all connections have been
		// validated. The previous runtime graph survives any decode failure.
		Clear();
		CanvasOffsetX = document.CanvasOffsetX;
		CanvasOffsetY = document.CanvasOffsetY;
		CanvasScale = document.CanvasScale;
		foreach (STNode node in document.Nodes)
			_Nodes.Add(node);
		foreach (STNode node in _Nodes)
		{
			node.OnEditorLoadCompleted();
		}
	}

	public void Clear()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_Nodes.Clear();
		CanvasOffsetX = 10f;
		CanvasOffsetY = 10f;
		CanvasScale = 1f;
	}

	public void SaveCanvas(string fileName)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		using FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
		SaveCanvas(stream);
	}

	public void SaveCanvas(Stream stream)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		STNode[] nodes = _Nodes.Cast<STNode>().ToArray();
		STNodeCanvasWriter.Write(
			stream,
			nodes,
			STNodeCanvasWriter.GetConnections(nodes),
			CanvasOffsetX,
			CanvasOffsetY,
			CanvasScale);
	}

	public byte[] GetCanvasData()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		using var stream = new MemoryStream();
		SaveCanvas(stream);
		return stream.ToArray();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_Nodes.Clear();
		_disposed = true;
		GC.SuppressFinalize(this);
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
