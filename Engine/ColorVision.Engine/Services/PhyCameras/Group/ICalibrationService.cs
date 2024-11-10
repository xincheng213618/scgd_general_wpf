using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public interface ICalibrationService<T>
    {
        public ObservableCollection<T> VisualChildren { get; set; }

        public SysResourceModel SysResourceModel { get; set; }
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; }
        public void AddChild(T t);
    }
}
