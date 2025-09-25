using System;

namespace ST.Library.UI.NodeEditor;

public class STNodeHubOption : STNodeOption
{
	public STNodeHubOption(string strText, Type dataType, bool bSingle)
		: base(strText, dataType, bSingle)
	{
	}

	public override ConnectionStatus ConnectOption(STNodeOption op)
	{
		Type num = typeof(object);
		if (base.DataType != num)
		{
			return base.ConnectOption(op);
		}
		base.DataType = op.DataType;
		ConnectionStatus connectionStatus = base.ConnectOption(op);
		if (connectionStatus != ConnectionStatus.Connected)
		{
			base.DataType = num;
		}
		return connectionStatus;
	}

	public override ConnectionStatus CanConnect(STNodeOption op)
	{
		if (op == STNodeOption.Empty)
		{
			return ConnectionStatus.EmptyOption;
		}
		if (base.DataType != typeof(object))
		{
			return base.CanConnect(op);
		}
		if (base.IsInput == op.IsInput)
		{
			return ConnectionStatus.SameInputOrOutput;
		}
		if (op.Owner == null || base.Owner == null)
		{
			return ConnectionStatus.NoOwner;
		}
		if (op.Owner == base.Owner)
		{
			return ConnectionStatus.SameOwner;
		}
		if (base.Owner.LockOption || op.Owner.LockOption)
		{
			return ConnectionStatus.Locked;
		}
		if (base.IsSingle && m_hs_connected.Count == 1)
		{
			return ConnectionStatus.SingleOption;
		}
		if (op.IsInput && STNodeEditor.CanFindNodePath(op.Owner, base.Owner))
		{
			return ConnectionStatus.Loop;
		}
		if (m_hs_connected.Contains(op))
		{
			return ConnectionStatus.Exists;
		}
		if (op.DataType == typeof(object))
		{
			return ConnectionStatus.ErrorType;
		}
		if (!base.IsInput)
		{
			return ConnectionStatus.Connected;
		}
		foreach (STNodeOption count in base.Owner.InputOptions)
		{
			foreach (STNodeOption item in count.ConnectedOption)
			{
				if (item == op)
				{
					return ConnectionStatus.Exists;
				}
			}
		}
		return ConnectionStatus.Connected;
	}
}
