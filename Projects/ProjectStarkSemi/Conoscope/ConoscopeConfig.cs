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

        // Global display settings (not model-specific)
        public bool IsShowRedChannel { get => _IsShowRedChannel; set { _IsShowRedChannel = value; OnPropertyChanged(); } }
        private bool _IsShowRedChannel;

        public bool IsShowGreenChannel { get => _IsShowGreenChannel; set { _IsShowGreenChannel = value; OnPropertyChanged(); } }
        private bool _IsShowGreenChannel;

        public bool IsShowBlueChannel { get => _IsShowBlueChannel; set { _IsShowBlueChannel = value; OnPropertyChanged(); } }
        private bool _IsShowBlueChannel;

        public bool IsShowXChannel { get => _IsShowXChannel; set { _IsShowXChannel = value; OnPropertyChanged(); } }
        private bool _IsShowXChannel;

        public bool IsShowYChannel { get => _IsShowYChannel; set { _IsShowYChannel = value; OnPropertyChanged(); } }
        private bool _IsShowYChannel = true;

        public bool IsShowZChannel { get => _IsShowZChannel; set { _IsShowZChannel = value; OnPropertyChanged(); } }
        private bool _IsShowZChannel;

        /// <summary>
        /// Whether to allow multiple channel selection (true) or single selection only (false)
        /// </summary>
        public bool AllowMultipleChannelSelection { get => _AllowMultipleChannelSelection; set { _AllowMultipleChannelSelection = value; OnPropertyChanged(); } }
        private bool _AllowMultipleChannelSelection = true;

        // ReferenceLineParam is model-specific, store it in profile
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ReferenceLineParam ReferenceLineParam
        {
            get => CurrentModelProfile.ReferenceLineParam;
            set
            {
                var profile = CurrentModelProfile;
                profile.ReferenceLineParam = value;
                OnPropertyChanged();
            }
        }

        // Backward compatibility: delegate to profile
        // These properties delegate to CurrentModelProfile for backward compatibility
        // TODO: Remove after migration or keep for compatibility with existing code

        /// <summary>
        /// MaxAngle for current model - delegates to profile
        /// </summary>
        [JsonIgnore]
        public int MaxAngle => CurrentModelProfile.MaxAngle;

        /// <summary>
        /// ConoscopeCoefficient for current model - delegates to profile
        /// </summary>
        [JsonIgnore]
        public double ConoscopeCoefficient => CurrentModelProfile.ConoscopeCoefficient;

        /// <summary>
        /// Whether current model has observation camera - delegates to profile
        /// </summary>
        [JsonIgnore]
        public bool HasObservationCamera => CurrentModelProfile.HasObservationCamera;

        /// <summary>
        /// Default angles for current model - delegates to profile
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<double> DefaultAngles => CurrentModelProfile.DefaultAngles;

        /// <summary>
        /// Default R angles for current model - delegates to profile
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<double> DefaultRAngles => CurrentModelProfile.DefaultRAngles;

        public ConoscopeConfig()
        {
            // Ensure default profiles exist
            EnsureProfile(ConoscopeModelType.VA60);
            EnsureProfile(ConoscopeModelType.VA80);
        }
    }
}
