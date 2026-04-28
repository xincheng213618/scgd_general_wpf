using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProjectStarkSemi.Conoscope
{
    public class ConoscopeConfig : ViewModelBase, IConfig
    {
        // CurrentModel 作为 Key，同时触发 ModelTypeChanged
        public ConoscopeModelType CurrentModel
        {
            get => _CurrentModel;
            set
            {
                if (_CurrentModel == value) return;
                _CurrentModel = value;
                OnPropertyChanged();
                // Ensure the profile exists
                EnsureProfile(value);
                // Notify subscribers
                ModelTypeChanged?.Invoke(this, _CurrentModel);
            }
        }
        private ConoscopeModelType _CurrentModel = ConoscopeModelType.VA60;

        public event EventHandler<ConoscopeModelType> ModelTypeChanged;

        // Model-specific configuration profiles
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<ConoscopeModelProfile> ModelProfiles
        {
            get => _ModelProfiles;
            set { _ModelProfiles = value; OnPropertyChanged(); }
        }
        private ObservableCollection<ConoscopeModelProfile> _ModelProfiles = new();

        /// <summary>
        /// Current model profile accessor
        /// </summary>
        public ConoscopeModelProfile CurrentModelProfile
        {
            get
            {
                EnsureProfile(CurrentModel);
                return ModelProfiles.FirstOrDefault(p => p.ModelType == CurrentModel)!;
            }
        }

        /// <summary>
        /// Ensure profile exists for given model type
        /// </summary>
        private void EnsureProfile(ConoscopeModelType modelType)
        {
            if (!ModelProfiles.Any(p => p.ModelType == modelType))
            {
                ModelProfiles.Add(ConoscopeModelProfile.CreateDefault(modelType));
            }
        }

        public ExportChannel DisplayChannel { get => _DisplayChannel; set { _DisplayChannel = value; OnPropertyChanged(); } }
        private ExportChannel _DisplayChannel = ExportChannel.Y;

        public bool ApplyFilterOnOpen { get => _ApplyFilterOnOpen; set { _ApplyFilterOnOpen = value; OnPropertyChanged(); } }
        private bool _ApplyFilterOnOpen = true;

        public ColorDifferenceReferenceMode ColorDifferenceReferenceMode { get => _ColorDifferenceReferenceMode; set { _ColorDifferenceReferenceMode = value; OnPropertyChanged(); } }
        private ColorDifferenceReferenceMode _ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.D65;

        public double ColorDifferenceCustomU { get => _ColorDifferenceCustomU; set { _ColorDifferenceCustomU = value; OnPropertyChanged(); } }
        private double _ColorDifferenceCustomU = 0.1978;

        public double ColorDifferenceCustomV { get => _ColorDifferenceCustomV; set { _ColorDifferenceCustomV = value; OnPropertyChanged(); } }
        private double _ColorDifferenceCustomV = 0.4684;


        public ConoscopeConfig()
        {
            EnsureProfile(ConoscopeModelType.VA60);
            EnsureProfile(ConoscopeModelType.VA80);
        }
    }
}
