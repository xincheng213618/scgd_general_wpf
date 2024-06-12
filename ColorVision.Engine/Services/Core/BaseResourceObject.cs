#pragma warning disable CS8625
using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Core
{
    public class BaseResourceObject : ViewModelBase
    {
        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public ObservableCollection<BaseResourceObject> VisualChildren { get; set; }
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

        public virtual string Name { get; set; }


        public virtual void Save()
        {
        }

        public virtual void Delete()
        { 
        }
    }
}
