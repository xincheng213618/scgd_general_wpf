using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectStarkSemi.Conoscope
{
    public class ConoscopeConfig : ViewModelBase, IConfig
    {
        public ConoscopeModelType CurrentModel { get => _CurrentModel; set { _CurrentModel = value; OnPropertyChanged(); } }
        private ConoscopeModelType _CurrentModel = ConoscopeModelType.VA80;

        public double ConoscopeCoefficient { get => _ConoscopeCoefficient; set { _ConoscopeCoefficient = value; OnPropertyChanged(); } }
        private double _ConoscopeCoefficient = 0.02645;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<double> DefaultAngles { get => _DefaultAngles; set { _DefaultAngles = value; OnPropertyChanged(); } }
        private ObservableCollection<double> _DefaultAngles = new ObservableCollection<double>() { 0,20,40,90,110,130,150 };

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<double> DefaultRAngles { get => _DefaultRAngles; set { _DefaultRAngles = value; OnPropertyChanged(); } }
        private ObservableCollection<double> _DefaultRAngles = new ObservableCollection<double>() {10 };

        public ReferenceLineParam ReferenceLineParam { get => _ReferenceLineParam; set { _ReferenceLineParam = value; OnPropertyChanged(); } }
        private ReferenceLineParam _ReferenceLineParam = new ReferenceLineParam();
    }
}
