using ColorVision.Common.Extension;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Common.MVVM;

namespace ColorVision.Solution.V
{

    public static class VObjectExtensions
    {
        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this VObject This) where T : VObject
        {
            if (This is T t)
                return t;

            if (This.Parent == null)
                return null;

            return This.Parent.GetAncestor<T>();
        }
    }

    public class VObject : INotifyPropertyChanged
    {
        public VObject Parent { get; set; }

        public virtual ObservableCollection<VObject> VisualChildren { get; set; }
        public virtual ObservableCollection<VObject> VisualChildrenHidden { get; set; }

        public event EventHandler AddChildEventHandler;

        public virtual void AddChild(VObject vObject)
        {
            if (vObject == null) return;
            vObject.Parent = this;
            AddChildEventHandler?.Invoke(this, new EventArgs());

            if (vObject.Visibility == Visibility.Visible)
                VisualChildren.SortedAdd(vObject);
            else
                VisualChildrenHidden.SortedAdd(vObject);

        }
        public event EventHandler RemoveChildEventHandler;
        public virtual void RemoveChild(VObject vObject)
        {
            RemoveChildEventHandler?.Invoke(this, new EventArgs());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public virtual string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;

        public virtual bool IsEditMode
        {
            get  => _IsEditMode;
            set {_IsEditMode = value; NotifyPropertyChanged(); }
        }
        private bool _IsEditMode ;

        public virtual string ToolTip { get => _ToolTip; set { _ToolTip = value; NotifyPropertyChanged(); } }
        private string _ToolTip = string.Empty;

        public virtual ImageSource Icon { get; set; }

        public RelayCommand AddChildren { get; set; }
        public RelayCommand RemoveChildren { get; set; }

        public RelayCommand DeleteCommand { get; set; }

        public RelayCommand OpenCommand { get; set; }

        public RelayCommand AttributesCommand { get; set; }

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded;

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public ContextMenu ContextMenu { get; set; }


        public VObject()
        {
            VisualChildren = new ObservableCollection<VObject>() { };
            VisualChildrenHidden = new ObservableCollection<VObject>() { };
            DeleteCommand = new RelayCommand((s) => Delete(), (s) => { return Parent != null && CanDelete; });
            OpenCommand = new RelayCommand((s) => Open(), (s) => { return Parent != null; });
        }


        private Visibility _Visibility = Visibility.Visible;

        public virtual Visibility Visibility
        {
            get => _Visibility;
            set
            {
                if (value != _Visibility)
                {
                    _Visibility = value;
                    if (Parent is VObject objects)
                    {
                        if (_Visibility == Visibility.Visible)
                        {
                            objects.VisualChildrenHidden.Remove(this);
                            objects.VisualChildren.SortedAdd(this);
                        }
                        else
                        {
                            objects.VisualChildren.Remove(this);
                            objects.VisualChildrenHidden.SortedAdd(this);
                        }
                    }
                    NotifyPropertyChanged();
                }
            }
        }

        public virtual void Delete()
        {
            if (Parent == null)
                return;

            if (MessageBox.Show("即将删除文件", "Grid", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Parent.RemoveChild(this);
            };
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

        public virtual void ReName()
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
