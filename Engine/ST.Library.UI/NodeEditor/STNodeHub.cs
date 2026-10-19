using System;
using System.Collections.Generic;
using System.Drawing;

namespace ST.Library.UI.NodeEditor;

public class STNodeHub : STNode, ISTNodeHub
{
    private readonly STNodeHubPorts ports;

    public STNodeHub() : this(false, "IN", "OUT", "HUB") { }
    public STNodeHub(bool bSingle) : this(bSingle, "IN", "OUT", "HUB") { }
    public STNodeHub(bool bSingle, string title) : this(bSingle, "IN", "OUT", title) { }

    public STNodeHub(bool bSingle, string strTextIn, string strTextOut, string title)
    {
        ports = new STNodeHubPorts(this, bSingle, strTextIn, strTextOut, input_DataTransfer,
            input_Connected, input_DisConnected, output_Connected, output_DisConnected);
        Title = Lang.Get(title);
        AutoSize = false;
        TitleColor = Color.FromArgb(200, Color.DarkOrange);
    }

    protected override void OnOwnerChanged()
    {
        base.OnOwnerChanged();
        if (Owner == null) return;
        using Graphics g = Owner.CreateGraphics();
        Width = GetDefaultNodeSize(g).Width;
    }

    protected virtual void output_DisConnected(object sender, STNodeOptionEventArgs e) => ports.OutputDisconnected(sender, e);
    protected virtual void output_Connected(object sender, STNodeOptionEventArgs e) => ports.OutputConnected(sender, e);
    protected virtual void input_DisConnected(object sender, STNodeOptionEventArgs e) => ports.InputDisconnected(sender, e);
    protected virtual void input_Connected(object sender, STNodeOptionEventArgs e) => ports.InputConnected(sender, e);
    protected virtual void input_DataTransfer(object sender, STNodeOptionEventArgs e) => ports.TransferData(sender, e);

    protected override void OnSaveNode(Dictionary<string, byte[]> dic)
    {
        base.OnSaveNode(dic);
        dic.Add("count", BitConverter.GetBytes(InputOptionsCount));
    }

    public override void OnLoadNode(Dictionary<string, byte[]> dic)
    {
        base.OnLoadNode(dic);
        ports.RestoreCount(BitConverter.ToInt32(dic["count"], 0));
    }
}
