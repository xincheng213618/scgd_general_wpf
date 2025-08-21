using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Abstractions
{
    public class DisplayAlgorithmParam
    {
        public Type Type { get; set; }
        public string?  ImageFilePath { get; set; }
    }

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
        public event EventHandler<DisplayAlgorithmParam> SelectParamChanged;

        public void SetType(DisplayAlgorithmParam param)
        {
            if (param == null) return;
            SelectParamChanged?.Invoke(this, param);
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
        public bool IsLocalFile { get; set; }

        public string ImageFilePath { get; set; }

        public UserControl GetUserControl();
    }

    public abstract class DisplayAlgorithmBase : ViewModelBase, IDisplayAlgorithm
    {
        public bool IsLocalFile { get => _IsLocalFile; set { _IsLocalFile = value; NotifyPropertyChanged(); } }
        private bool _IsLocalFile;

        public string ImageFilePath { get => _ImageFilePath; set { _ImageFilePath = value; NotifyPropertyChanged(); } }
        private string _ImageFilePath;



        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }
    };
}
