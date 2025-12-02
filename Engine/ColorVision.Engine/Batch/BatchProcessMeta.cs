using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Linq;

namespace ColorVision.Engine.Batch
{
    public class BatchProcessMeta:ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string TemplateName { get => _TemplateName; set { _TemplateName = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExecutionOrder)); } }
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
        /// Gets the execution order of this process within its flow template.
        /// This shows the order in which processes with the same TemplateName will be executed.
        /// </summary>
        [JsonIgnore]
        public string ExecutionOrder
        {
            get
            {
                var manager = BatchManager.GetInstance();
                if (manager == null || string.IsNullOrEmpty(TemplateName))
                    return string.Empty;

                var sameTemplateItems = manager.ProcessMetas
                    .Where(m => string.Equals(m.TemplateName, TemplateName, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (sameTemplateItems.Count <= 1)
                    return string.Empty;

                int index = sameTemplateItems.IndexOf(this);
                return index >= 0 ? $"{index + 1}/{sameTemplateItems.Count}" : string.Empty;
            }
        }

        /// <summary>
        /// Notifies that the execution order may have changed.
        /// </summary>
        public void NotifyExecutionOrderChanged()
        {
            OnPropertyChanged(nameof(ExecutionOrder));
        }

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
