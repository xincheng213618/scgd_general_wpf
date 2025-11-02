using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Engine.Batch
{
    public class BatchProcessMeta:ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string TemplateName { get => _TemplateName; set { _TemplateName = value; OnPropertyChanged(); } }
        private string _TemplateName;

        public IBatchProcess BatchProcess 
        { 
            get => _BatchProcess; 
            set 
            { 
                _BatchProcess = value; 
                _cachedMetadata = null; // Clear cache when process changes
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ProcessTypeName)); 
                OnPropertyChanged(nameof(ProcessDisplayName));
                OnPropertyChanged(nameof(ProcessDescription));
                OnPropertyChanged(nameof(ProcessCategory));
                OnPropertyChanged(nameof(Metadata));
            } 
        }
        private IBatchProcess _BatchProcess;

        private BatchProcessMetadata? _cachedMetadata;

        public string ProcessTypeName => BatchProcess?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => BatchProcess?.GetType().FullName ?? string.Empty;

        /// <summary>
        /// Gets the metadata for the batch process.
        /// </summary>
        [JsonIgnore]
        public BatchProcessMetadata Metadata
        {
            get
            {
                if (_cachedMetadata == null && BatchProcess != null)
                {
                    _cachedMetadata = BatchProcessMetadata.FromProcess(BatchProcess);
                }
                return _cachedMetadata ?? new BatchProcessMetadata();
            }
        }

        /// <summary>
        /// Gets the display name from metadata (falls back to type name if no metadata).
        /// </summary>
        [JsonIgnore]
        public string ProcessDisplayName => Metadata.DisplayName;

        /// <summary>
        /// Gets the description from metadata.
        /// </summary>
        [JsonIgnore]
        public string ProcessDescription => Metadata.Description;

        /// <summary>
        /// Gets the category from metadata.
        /// </summary>
        [JsonIgnore]
        public string ProcessCategory => Metadata.Category;
    }
}
