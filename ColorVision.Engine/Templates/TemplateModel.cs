using ColorVision.UI.Sorts;
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates
{
    public class TemplateModelBase : ViewModelBase, ISortID, ISortKey
    {
        public ContextMenu ContextMenu { get; set; }
        public virtual int Id { get; set; }
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;
        public virtual string Key { get; set; }

        public virtual bool IsEditMode { get => _IsEditMode; set { _IsEditMode = value; NotifyPropertyChanged(); } }
        private bool _IsEditMode;

        public virtual object GetValue()
        {
            throw new NotImplementedException();
        }
    }

    public class TemplateModel<T> : TemplateModelBase where T : ParamBase
    {
        public RelayCommand ReNameCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }
        public IList<TemplateModel<T>> Parent { get; set; }

        public TemplateModel() : base()
        {
            ReNameCommand = new RelayCommand(a => IsEditMode = true);
            DeleteCommand = new RelayCommand(a => Parent?.Remove(this), a => Parent != null);
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuRename, InputGestureText = "F2", Command = ReNameCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Delete, InputGestureText = "F2", Command = DeleteCommand });
        }

        public TemplateModel(string Key, T Value) :base()
        {
            this.Value = Value;
            this.Key = Key;
            ReNameCommand = new RelayCommand(a => IsEditMode = true);
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuRename, InputGestureText = "F2", Command = ReNameCommand });
        }
        public TemplateModel(KeyValuePair<string, T> keyValuePair)
        {
            Key = keyValuePair.Key;
            Value = keyValuePair.Value;
        }



        [JsonIgnore]
        public override int Id { get => Value.Id; }

        [JsonIgnore]
        public override string Key
        {
            get => Value.Name;
            set
            {
                Value.Name = value; NotifyPropertyChanged();
            }
        }

        public T Value { get; set; }

        public override object GetValue()
        {
            return Value;
        }


    }
}
