#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Core
{
    public class ServiceObjectBase : ViewModelBase
    {
        [CommandDisplay("删除")]
        public RelayCommand DeleteCommand { get; set; }

        [CommandDisplay("保存")]
        public RelayCommand SaveCommand { get; set; }

        public ObservableCollection<ServiceObjectBase> VisualChildren { get; set; }
        public ServiceObjectBase()
        {
            VisualChildren = new ObservableCollection<ServiceObjectBase>();
            SaveCommand = new RelayCommand(a => Save());
            DeleteCommand = new RelayCommand(a => Delete(), a => AccessControl.Check(PermissionMode.Administrator));
        }
        public ServiceObjectBase Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                NotifyPropertyChanged();
            }
        }
        private ServiceObjectBase _Parent;

        public virtual void AddChild(ServiceObjectBase baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = this;
            VisualChildren.SortedAdd(baseObject);
        }
        public virtual void RemoveChild(ServiceObjectBase baseObject)
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
