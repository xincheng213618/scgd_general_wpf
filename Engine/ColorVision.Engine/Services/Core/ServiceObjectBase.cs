#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System;

namespace ColorVision.Engine.Services
{
    public class ServiceObjectBase : ViewModelBase
    {
        protected virtual string? AuthorizationResourceId => null;

        internal bool CanDelete => AccessControl.Evaluate("resource.delete", PermissionMode.Administrator, AuthorizationResourceId).Allowed;

        protected void DemandDeleteAccess() => AccessControl.Demand("resource.delete", PermissionMode.Administrator, AuthorizationResourceId);

        protected void DeleteFromCommand()
        {
            try { Delete(); }
            catch (AccessDeniedException)
            {
                MessageBox.Show(EngineLocalization.Get("当前权限不足，无法删除此资源。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [CommandDisplay("Delete", CommandType = CommandType.Highlighted, Order = 99999, CategoryOrder = 4)]
        [Category("MaintenanceDiagnostics")]
        [Description("CommandDeleteHint")]
        public RelayCommand DeleteCommand { get; set; }

        [CommandDisplay("Save",Order =-11),BrowsableAttribute(false)]
        public RelayCommand SaveCommand { get; set; }

        public ObservableCollection<ServiceObjectBase> VisualChildren { get; set; }
        public ServiceObjectBase()
        {
            VisualChildren = new ObservableCollection<ServiceObjectBase>();
            SaveCommand = new RelayCommand(a => Save());
            DeleteCommand = new RelayCommand(a => DeleteFromCommand(), a => CanDelete);
        }
        public ServiceObjectBase Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                OnPropertyChanged();
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
            DemandDeleteAccess();
        }
    }
}
