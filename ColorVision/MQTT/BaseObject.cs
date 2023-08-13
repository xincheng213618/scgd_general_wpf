#pragma warning disable CS8625
using ColorVision.Extension;
using ColorVision.MQTT.Service;
using ColorVision.MVVM;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.MQTT
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

    public class BaseObject : ViewModelBase
    {
        public RelayCommand SaveCommand { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public ObservableCollection<BaseObject> VisualChildren { get; set; }

        public ServiceControl ServiceControl { get; set; }
        public BaseObject()
        {
            VisualChildren = new ObservableCollection<BaseObject>();
            SaveCommand = new RelayCommand(a => Save());
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

        public virtual string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

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

        public virtual void Save()
        {
        }

    }
}
