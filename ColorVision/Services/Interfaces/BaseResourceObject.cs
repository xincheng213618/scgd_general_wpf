#pragma warning disable CS8625
using ColorVision.Common.Extension;
using ColorVision.MVVM;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Services.Devices
{

    public interface ITreeViewItem
    {
        public bool IsExpanded { get; set; }
        public ContextMenu ContextMenu { get; set; }


    }


    public class BaseResourceObject : ViewModelBase,ITreeViewItem
    {
        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public ObservableCollection<BaseResourceObject> VisualChildren { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public BaseResourceObject()
        {
            VisualChildren = new ObservableCollection<BaseResourceObject>();
            SaveCommand = new RelayCommand(a => Save());
            DeleteCommand = new RelayCommand(a => Delete());
        }
        public BaseResourceObject Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                NotifyPropertyChanged();
            }
        }
        private BaseResourceObject _Parent;

        public virtual void AddChild(BaseResourceObject baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = this;
            VisualChildren.SortedAdd(baseObject);
        }
        public virtual void RemoveChild(BaseResourceObject baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = null;
            VisualChildren.Remove(baseObject);
        }

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded = true;


        public virtual string Name { get; set; }


        public virtual void Save()
        {
        }

        public virtual void Delete()
        { 
        }
    }
}
