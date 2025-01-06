using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public class ParamModBase : ModelBase
    {
        [Browsable(false)]
        [JsonIgnore]
        public virtual RelayCommand CreateCommand { get; set; }

        [Browsable(false)]
        public ObservableCollection<ModDetailModel> ModDetailModels { get; set; } = new ObservableCollection<ModDetailModel>();

        public ParamModBase() : base(new List<ModDetailModel>())
        {
        }

        public ParamModBase(int id, string name, List<ModDetailModel> detail) : base(detail)
        {
            Id = id;
            Name = name;
            ModDetailModels = new ObservableCollection<ModDetailModel>(detail);
            CreateCommand = new RelayCommand(a => new CreateModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }
        [Browsable(false)]
        public ModMasterModel ModMaster { get; set; }

        public ParamModBase(ModMasterModel modMaster, List<ModDetailModel> detail) : base(detail)
        {
            Id = modMaster.Id;
            Name = modMaster.Name ?? string.Empty;
            ModMaster = modMaster;
            ModDetailModels = new ObservableCollection<ModDetailModel>(detail);
            CreateCommand = new RelayCommand(a => new CreateModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }

    }
}
