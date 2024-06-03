using ColorVision.Engine.Templates;
using ColorVision.Services.Dao;
using System.Collections.ObjectModel;

namespace ColorVision.Services.PhyCameras.Templates
{
    public interface ICalibrationService<T>
    {
        public ObservableCollection<T> VisualChildren { get; set; }

        public SysResourceModel SysResourceModel { get; set; }
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; }
        public void AddChild(T t);
    }
}
