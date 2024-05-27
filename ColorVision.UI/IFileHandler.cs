using ColorVision.Common.Utilities;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public interface IFileHandler
    {
        int Order { get; }
        bool CanHandle(string filePath);
        void Handle(string filePath);
    }

    public class FileHandlerProcessor
    {
        private static FileHandlerProcessor _instance;
        private static readonly object _locker = new();
        public static FileHandlerProcessor GetInstance() { lock (_locker) { return _instance ??= new FileHandlerProcessor(); } }

        private readonly List<IFileHandler> _fileHandlers;

        public FileHandlerProcessor()
        {
            _fileHandlers = new List<IFileHandler>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IFileHandler).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IFileHandler fileHandler)
                    {
                        _fileHandlers.Add(fileHandler);
                    }
                }
            }
            _fileHandlers = _fileHandlers.OrderBy(handler => handler.Order).ToList();
        }

        public bool ProcessFile(string filePath)
        {
            foreach (var handler in _fileHandlers)
            {
                if (handler.CanHandle(filePath))
                {
                    handler.Handle(filePath);
                    return true;
                }
            }
            return false;
        }

    }

    //public class DefaultFileHandler : IFileHandler
    //{
    //    public int Order => 4;

    //    public bool CanHandle(string filePath)
    //    {
    //        return File.Exists(filePath);
    //    }
    //    public void Handle(string filePath)
    //    {
    //        PlatformHelper.Open(filePath);
    //    }
    //}
}
