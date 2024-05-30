using ColorVision.UI.Sorts;
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.Services.Templates
{
    public class TemplateModelBase : ViewModelBase, ISortID,ISortKey
    {
        public ContextMenu ContentMenu { get; set; }

        public RelayCommand ReNameCommand { get; set; }

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

        public TemplateModel()
        {
            ReNameCommand = new RelayCommand(a => IsEditMode = true);
            ContentMenu = new ContextMenu();
            ContentMenu.Items.Add(new MenuItem() { Header = Engine.Properties.Resources.MenuRename, InputGestureText = "F2", Command = ReNameCommand });
        }

        public TemplateModel(string Key, T Value)
        {
            this.Value = Value;
            this.Key = Key;
            ReNameCommand = new RelayCommand(a => IsEditMode = true);
            ContentMenu = new ContextMenu();
            ContentMenu.Items.Add(new MenuItem() { Header = Engine.Properties.Resources.MenuRename, InputGestureText = "F2", Command = ReNameCommand });
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
