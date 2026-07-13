using ColorVision.Common.MVVM;
using System.IO;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public sealed class BatchImageItem : ViewModelBase
    {
        public BatchImageItem(string filePath, string? sourceRoot = null)
        {
            FilePath = filePath;
            SourceRoot = sourceRoot;
        }

        public string FilePath { get; }

        public string FileName => Path.GetFileName(FilePath);

        public string? SourceRoot { get; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }
        private string _status = "等待处理";

        public string? OutputPath
        {
            get => _outputPath;
            set
            {
                _outputPath = value;
                OnPropertyChanged();
            }
        }
        private string? _outputPath;
    }
}
