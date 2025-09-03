using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class FlowNodeLoader : FlowEngineControl
{
	public FlowNodeLoader(bool isAutoStartName)
		: base(isAutoStartName)
	{
	}

	public FlowNodeLoader(string fileName)
		: this(fileName, isAutoStartName: true)
	{
	}

	public FlowNodeLoader(string fileName, bool isAutoStartName)
		: this(isAutoStartName)
	{
		STNodeEditor sTNodeEditor = new STNodeEditor();
		sTNodeEditor.LoadAssembly(fileName);
		AttachNodeEditor(sTNodeEditor);
	}

	public bool LoadAssemblyFromBase64(string base64)
	{
		STNodeEditor sTNodeEditor = new STNodeEditor();
		if (sTNodeEditor.LoadAssemblyFromBase64(base64))
		{
			AttachNodeEditor(sTNodeEditor);
			return true;
		}
		return false;
	}
}
