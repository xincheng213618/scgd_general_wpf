#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates
{
    public class TemplateConfig : ViewModelBase, IConfig
    {
        public static TemplateConfig Instance => ConfigHandler.GetInstance().GetRequiredService<TemplateConfig>();

        public string DefaultCreateTemplateName { get => _DefaultCreateTemplateName; set { _DefaultCreateTemplateName = value; NotifyPropertyChanged(); } }
        private string _DefaultCreateTemplateName = Properties.Resources.DefaultCreateTemplateName;

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }
}
