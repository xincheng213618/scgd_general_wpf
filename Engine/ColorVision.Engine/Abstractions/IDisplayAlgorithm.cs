using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Abstractions
{
    public class DisplayAlgorithmManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisplayAlgorithmManager));
        private static DisplayAlgorithmManager _instance;
        private static readonly object _locker = new();
        public static DisplayAlgorithmManager GetInstance() { lock (_locker) { _instance ??= new DisplayAlgorithmManager(); return _instance; } }
        public ObservableCollection<IResultHandleBase> ResultHandles { get; set; }

        public DisplayAlgorithmManager()
        {
            ResultHandles = new ObservableCollection<IResultHandleBase>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IResultHandleBase).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IResultHandleBase algorithmResultRender)
                    {
                        ResultHandles.Add(algorithmResultRender);
                    }
                }
            }
        }
        public event EventHandler<Type> SelectTypeChanged;

        public void SetType(Type type)
        {
            if (type == null) return;
            SelectTypeChanged?.Invoke(this, type);
        }

        public event EventHandler<string> SelectFileNameChanged;

        public void SetFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            SelectFileNameChanged?.Invoke(this, fileName);
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DisplayAlgorithmAttribute : Attribute
    {
        public int Order { get; }
        public string Name { get; }
        public string Group { get; }

        public DisplayAlgorithmAttribute(int order, string name, string group)
        {
            Order = order;
            Name = name;
            Group = group;
        }
    }

    public interface IDisplayAlgorithm
    {
        public int Order { get; set; }
        public string Name { get; set; }

        public string Group { get; set; }
        public UserControl GetUserControl();
    }

    public abstract class DisplayAlgorithmBase : ViewModelBase, IDisplayAlgorithm
    {
        public virtual string Name { get; set; }
        public virtual int Order { get; set; }

        public virtual string Group { get; set; }

        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }
    };
}
