using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates
{
    public class TemplateBase : ViewModelBase
    {
        [JsonIgnore]
        public ContextMenu ContextMenu { get; set; }
        [JsonIgnore]
        public virtual int Id { get; set; }

        [JsonIgnore]
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; OnPropertyChanged(); } }
        private bool _IsSelected;

        public virtual string Key { get; set; }

        [JsonIgnore]
        public virtual bool IsEditMode { get => _IsEditMode; set { _IsEditMode = value; OnPropertyChanged(); } }
        private bool _IsEditMode;

        public virtual object GetValue()
        {
            throw new NotImplementedException();
        }
    }

    public class TemplateModel<T> : TemplateBase where T : ParamBase
    {
        public RelayCommand ReNameCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }

        public RelayCommand CopyNameCommand { get; set; }


        public TemplateModel(string Key, T Value) :base()
        {
            this.Value = Value;
            this.Key = Key;
            ReNameCommand = new RelayCommand(a => IsEditMode = true);
            CopyNameCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(Key));
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuRename, InputGestureText = "F2", Command = ReNameCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "复制名称", Command = CopyNameCommand });

        }

        [JsonIgnore]
        public override int Id { get => Value.Id; }

        [JsonIgnore]
        public override string Key
        {
            get => Value.Name;
            set
            {
                Value.Name = value; OnPropertyChanged();
            }
        }

        public T Value { get; set; }

        public override object GetValue()
        {
            return Value;
        }


    }
}
