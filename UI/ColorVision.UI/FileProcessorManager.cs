namespace ColorVision.UI
{
    public class FileProcessorManager
    {
        private static FileProcessorManager _instance;
        private static readonly object _locker = new();
        public static FileProcessorManager GetInstance() { lock (_locker) { return _instance ??= new FileProcessorManager(); } }

        private readonly List<IFileProcessor> _fileProcessors;

        public FileProcessorManager()
        {
            _fileProcessors = new List<IFileProcessor>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IFileProcessor).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IFileProcessor fileHandler)
                    {
                        _fileProcessors.Add(fileHandler);
                    }
                }
            }
            _fileProcessors = [.. _fileProcessors.OrderBy(handler => handler.Order)];
        }


        public bool HandleFile(string filePath)
        {
            foreach (var processor in _fileProcessors)
            {
                if (processor.CanProcess(filePath))
                {
                    processor.Process(filePath);
                    return true;
                }
            }
            return false;
        }


        public bool ExportFile(string filePath)
        {
            foreach (var processor in   _fileProcessors)
            {
                if (processor.CanExport(filePath))
                {
                    processor.Export(filePath);
                    return true;
                }
            }
            return false;
        }

    }
}
