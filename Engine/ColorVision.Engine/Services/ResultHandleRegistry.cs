using ColorVision.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace ColorVision.Engine
{
    public sealed class ResultHandleRegistry
    {
        private static readonly Lazy<ResultHandleRegistry> _instance = new(() => new ResultHandleRegistry());

        public static ResultHandleRegistry GetInstance() => _instance.Value;

        public ObservableCollection<IResultHandleBase> ResultHandles { get; } = new();

        private ResultHandleRegistry()
        {
            foreach (Assembly assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(type =>
                             typeof(IResultHandleBase).IsAssignableFrom(type) && !type.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IResultHandleBase resultHandle)
                    {
                        ResultHandles.Add(resultHandle);
                    }
                }
            }
        }
    }
}
