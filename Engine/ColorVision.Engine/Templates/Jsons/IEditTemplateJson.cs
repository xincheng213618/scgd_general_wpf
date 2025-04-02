using ColorVision.Common.MVVM;
using System;

namespace ColorVision.Engine.Templates.Jsons
{
    public interface IEditTemplateJson
    {
        public RelayCommand ResetCommand { get; set; }
        public RelayCommand CheckCommand { get; set; }
        public string Description  { get; }

        public RelayCommand OpenEditToolCommand { get; set; }
        public string JsonValue { get; set; }
        public event EventHandler JsonValueChanged;
    }
}
