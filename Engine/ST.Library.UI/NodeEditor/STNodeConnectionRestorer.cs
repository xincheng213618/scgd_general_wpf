using System;
using System.Collections.Generic;
using System.Linq;

namespace ST.Library.UI.NodeEditor;

internal static class STNodeConnectionRestorer
{
    public static ConnectionStatus Restore(
        IEnumerable<(STNodeOption Output, STNodeOption Input)> connections,
        bool isOwnerOfOwner)
    {
        var pending = connections.ToList();
        while (pending.Count > 0)
        {
            var deferred = new List<(STNodeOption Output, STNodeOption Input)>();
            foreach (var connection in pending)
            {
                // Hub port types are inferred from restored connections, not saved in the node payload.
                // Restore their typed neighbours first; drawing order must not decide whether a graph loads.
                if (connection.Output is STNodeHubOption && connection.Input is STNodeHubOption
                    && connection.Output.DataType == typeof(object) && connection.Input.DataType == typeof(object))
                {
                    deferred.Add(connection);
                    continue;
                }

                STNode outputNode = connection.Output.Owner;
                STNode inputNode = connection.Input.Owner;
                bool outputLocked = outputNode.LockOption;
                bool inputLocked = inputNode.LockOption;
                outputNode.LockOption = false;
                inputNode.LockOption = false;
                try
                {
                    ConnectionStatus status = connection.Output.ConnectOption(connection.Input, isOwnerOfOwner);
                    if (status != ConnectionStatus.Connected)
                        return status;
                }
                finally
                {
                    outputNode.LockOption = outputLocked;
                    inputNode.LockOption = inputLocked;
                }
            }

            // No typed neighbour can resolve the remaining edges. Keep ordinary type validation strict.
            if (deferred.Count == pending.Count)
                return ConnectionStatus.ErrorType;
            pending = deferred;
        }
        return ConnectionStatus.Connected;
    }
}
