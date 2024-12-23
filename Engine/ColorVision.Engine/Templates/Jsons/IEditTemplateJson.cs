using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Templates.Jsons
{
    public interface IEditTemplateJson
    {
        public RelayCommand ResetCommand { get; set; }
        public string JsonValue { get; set; }
    }
}
