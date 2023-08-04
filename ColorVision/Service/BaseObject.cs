using ColorVision.Extension;
using ColorVision.MVVM;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Template
{
    public class BaseObject : ViewModelBase
    {

        public ContextMenu ContextMenu { get; set; }
        public  ObservableCollection<BaseObject> VisualChildren { get; set; }
        public BaseObject()
        {
            VisualChildren = new ObservableCollection<BaseObject>();
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
