using System;

namespace ST.Library.UI.NodeEditor;

/// <summary>Paired hub ports shared by relay nodes and specialized execution nodes.</summary>
public sealed class STNodeHubPorts
{
    private readonly STNode node;
    private readonly bool single;
    private readonly string inputText;
    private readonly string outputText;
    private readonly STNodeOptionEventHandler inputTransfer;
    private readonly STNodeOptionEventHandler inputConnected;
    private readonly STNodeOptionEventHandler inputDisconnected;
    private readonly STNodeOptionEventHandler outputConnected;
    private readonly STNodeOptionEventHandler outputDisconnected;

    public STNodeHubPorts(STNode node, bool single, string inputText, string outputText,
        STNodeOptionEventHandler inputTransfer, STNodeOptionEventHandler inputConnected = null,
        STNodeOptionEventHandler inputDisconnected = null, STNodeOptionEventHandler outputConnected = null,
        STNodeOptionEventHandler outputDisconnected = null)
    {
        this.node = node;
        this.single = single;
        this.inputText = inputText;
        this.outputText = outputText;
        this.inputTransfer = inputTransfer;
        this.inputConnected = inputConnected ?? InputConnected;
        this.inputDisconnected = inputDisconnected ?? InputDisconnected;
        this.outputConnected = outputConnected ?? OutputConnected;
        this.outputDisconnected = outputDisconnected ?? OutputDisconnected;
        Add();
    }

    private void Add()
    {
        var input = new STNodeHubOption(inputText, typeof(object), single);
        var output = new STNodeHubOption(outputText, typeof(object), false);
        node.InputOptions.Add(input);
        node.OutputOptions.Add(output);
        input.Connected += inputConnected;
        input.DisConnected += inputDisconnected;
        input.DataTransfer += inputTransfer;
        output.Connected += outputConnected;
        output.DisConnected += outputDisconnected;
        node.SetFixedSize(node.Width, node.TitleHeight + node.InputOptionsCount * 20);
    }

    public void RestoreCount(int count)
    {
        while (node.InputOptionsCount < count) Add();
    }

    public void InputConnected(object sender, STNodeOptionEventArgs e)
    {
        var input = (STNodeOption)sender;
        int index = node.InputOptions.IndexOf(input);
        if (input.DataType == typeof(object))
        {
            input.DataType = e.TargetOption.DataType;
            node.OutputOptions[index].DataType = input.DataType;
            EnsureSpare();
        }
        else
        {
            node.OutputOptions[index].TransferData(e.TargetOption.Data);
        }
    }

    public void OutputConnected(object sender, STNodeOptionEventArgs e)
    {
        var output = (STNodeOption)sender;
        int index = node.OutputOptions.IndexOf(output);
        if (node.InputOptions[index].DataType != typeof(object)) return;
        output.DataType = e.TargetOption.DataType;
        node.InputOptions[index].DataType = output.DataType;
        EnsureSpare();
    }

    private void EnsureSpare()
    {
        foreach (STNodeOption input in node.InputOptions)
            if (input.DataType == typeof(object)) return;
        Add();
    }

    public void InputDisconnected(object sender, STNodeOptionEventArgs e)
    {
        var input = (STNodeOption)sender;
        int index = node.InputOptions.IndexOf(input);
        if (input.ConnectionCount == 0 && node.OutputOptions[index].ConnectionCount == 0) Remove(index);
    }

    public void OutputDisconnected(object sender, STNodeOptionEventArgs e)
    {
        var output = (STNodeOption)sender;
        int index = node.OutputOptions.IndexOf(output);
        if (output.ConnectionCount == 0 && node.InputOptions[index].ConnectionCount == 0) Remove(index);
    }

    private void Remove(int index)
    {
        node.InputOptions.RemoveAt(index);
        node.OutputOptions.RemoveAt(index);
        node.Owner?.BuildLinePath();
        node.SetFixedSize(node.Width, node.Height - 20);
    }

    public void TransferData(object sender, STNodeOptionEventArgs e)
    {
        int index = node.InputOptions.IndexOf((STNodeOption)sender);
        node.OutputOptions[index].TransferData(e.Status == ConnectionStatus.Connected ? e.TargetOption.Data : null);
    }
}
