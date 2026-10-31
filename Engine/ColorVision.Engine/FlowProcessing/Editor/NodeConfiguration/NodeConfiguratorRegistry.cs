using ColorVision.UI;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration
{
    public static class NodeConfiguratorRegistry
    {
        private static readonly Lazy<ConcurrentDictionary<Type, INodeConfigurator>> _configurators = new(InitializeConfigurators);

        public static INodeConfigurator? GetConfigurator(Type nodeType)
        {
            if (_configurators.Value.TryGetValue(nodeType, out var configurator))
                return configurator;
            return null;
        }

        private static ConcurrentDictionary<Type, INodeConfigurator> InitializeConfigurators()
        {
            var dict = new ConcurrentDictionary<Type, INodeConfigurator>();

            foreach (var type in AssemblyHandler.Instance.GetImplementationTypes(typeof(INodeConfigurator)))
            {
                var attr = type.GetCustomAttribute<NodeConfiguratorAttribute>();
                if (attr != null && Activator.CreateInstance(type) is INodeConfigurator configurator)
                {
                    dict[attr.NodeType] = configurator;
                }
            }
            return dict;
        }
    }
}