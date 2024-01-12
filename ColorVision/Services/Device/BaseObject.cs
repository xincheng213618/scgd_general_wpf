#pragma warning disable CS8625
using ColorVision.Extension;
using ColorVision.MVVM;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Services.Device
{
    public static class BaseObjectExtensions
    {
        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this BaseObject This) where T : BaseObject
        {
            if (This is T t)
                return t;

            if (This.Parent == null)
                return null;

            return This.Parent.GetAncestor<T>();
        }
    }

    public interface ITreeViewItem
    {
        public bool IsExpanded { get; set; }
    }

    public class BaseObject : ViewModelBase,ITreeViewItem
    {
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public ObservableCollection<BaseObject> VisualChildren { get; set; }
        public ServiceManager ServiceControl { get; set; }
        public BaseObject()
        {
            VisualChildren = new ObservableCollection<BaseObject>();
            SaveCommand = new RelayCommand(a => Save());
            DeleteCommand = new RelayCommand(a => Delete());
        }
        public BaseObject Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                NotifyPropertyChanged();
            }
        }
        private BaseObject _Parent;

        public virtual void AddChild(BaseObject baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = this;
            VisualChildren.SortedAdd(baseObject);
        }
        public virtual void RemoveChild(BaseObject baseObject)
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

        public virtual void Delete() { }


    }
}
