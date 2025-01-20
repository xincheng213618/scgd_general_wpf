using ColorVision.Common.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using System.Runtime.Serialization;

namespace ColorVision.Solution.V
{

    [DataContract]
    public class VObject : INotifyPropertyChanged
    {
        public VObject Parent { get; set; }

        public virtual ObservableCollection<VObject> VisualChildren { get; set; }

        public event EventHandler AddChildEventHandler;

        public virtual void AddChild(VObject vObject)
        {
            if (vObject == null) return;
            vObject.Parent = this;
            AddChildEventHandler?.Invoke(this, new EventArgs());
            VisualChildren.SortedAdd(vObject);

        }
        public event EventHandler RemoveChildEventHandler;
        public virtual void RemoveChild(VObject vObject)
        {
            this.VisualChildren.Remove(vObject);
            RemoveChildEventHandler?.Invoke(this, new EventArgs());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public virtual string Name { get => _Name; set
            { 
                if (_Name == value) return; 
                if (!IsEditMode || ReName(value))
                {
                    _Name = value;
                }
                NotifyPropertyChanged();  
            } 
        }
        private string _Name = string.Empty;

        public virtual string FullPath { get => _FullPath; set { _FullPath = value; NotifyPropertyChanged(); } }
        private string _FullPath = string.Empty;

        public virtual bool IsEditMode
        {
            get  => _IsEditMode;
            set {_IsEditMode = value; NotifyPropertyChanged(); }
        }
        private bool _IsEditMode ;

        public virtual string ToolTip { get => _ToolTip; set { _ToolTip = value; NotifyPropertyChanged(); } }
        private string _ToolTip = string.Empty;

        public virtual ImageSource? Icon { get; set; }

        public RelayCommand AddChildrenCommand { get; set; }
        public RelayCommand RemoveChildrenCommand { get; set; }
        public RelayCommand OpenCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }

        public RelayCommand AttributesCommand { get; set; }

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded;

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public ContextMenu ContextMenu { get; set; }


        public VObject()
        {
            VisualChildren = new ObservableCollection<VObject>() { };
            OpenCommand = new RelayCommand((s) => Open());
            DeleteCommand = new RelayCommand(s =>Delete());
        }

        public virtual void Delete()
        {
            if (Parent == null)
                return;
            Parent.RemoveChild(this);
        }

        public virtual bool CanReName { get; set; } = true;
        public virtual bool CanDelete { get; set; } = true;
        public virtual bool CanAdd { get; set; } = true;
        public virtual bool CanCopy { get; set; } = true;
        public virtual bool CanPaste { get; set; } = true;
        public virtual bool CanCut { get; set; } = true;


        public void MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Open();
        }

        public virtual void Open()
        {
        }

        public virtual void Copy()
        {
            throw new NotImplementedException();
        }

        public virtual bool ReName(string name)
        {
            throw new NotImplementedException();
        }

        public virtual int CompareTo(object obj)
        {
            if (obj == null) return -1;
            else if (obj == this) return 0;
            else if (obj is VObject vObject) return Common.NativeMethods.Shlwapi.CompareLogical(Name, vObject.Name);
            else return -1;
        }
    }
}
