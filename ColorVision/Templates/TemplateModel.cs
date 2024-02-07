using ColorVision.MVVM;
using ColorVision.Sorts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.Templates
{
    public class TemplateModelBase : ViewModelBase,ISortID
    {
        public virtual int Id { get; set; }

        public virtual string Key { get; set; }
        public string Tag { get => _Tag; set { _Tag = value; NotifyPropertyChanged(); } }
        private string _Tag;

        public virtual bool IsEditMode { get => _IsEditMode;set { _IsEditMode =value; NotifyPropertyChanged(); }}
        private bool _IsEditMode;

        public virtual object GetValue()
        {
            throw new NotImplementedException();
        }
    }

    public class TemplateModel<T>: TemplateModelBase where T : ParamBase
    {
        public TemplateModel()
        {
        }
        public TemplateModel(string Key, T Value)
        {
            this.Value = Value;
            this.Key = Key;
        }
        public TemplateModel(KeyValuePair<string, T> keyValuePair)
        {
            Key = keyValuePair.Key;
            Value = keyValuePair.Value;
        }

        [JsonIgnore]
        public override int Id { get => Value.Id;}

        [JsonIgnore]
        public override string Key
        { 
            get =>  Value.Name;
            set { Value.Name = value;  NotifyPropertyChanged(); 
            } 
        }

        public T Value { get; set; }

        public override object GetValue()
        {
            return Value;
        }


    }
}
