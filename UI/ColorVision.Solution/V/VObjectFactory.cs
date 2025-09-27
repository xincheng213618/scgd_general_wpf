using ColorVision.Solution.FileMeta;
using ColorVision.Solution.FolderMeta;
using System.IO;

namespace ColorVision.Solution.V
{
    /// <summary>
    /// Factory for creating VObject instances using the new registry system.
    /// Demonstrates dependency injection and factory pattern usage for V objects.
    /// </summary>
    public static class VObjectFactory
    {
        /// <summary>
        /// Create a VFolder using the new FolderMetaRegistry system
        /// </summary>
        /// <param name="directoryInfo">Directory information</param>
        /// <returns>Configured VFolder instance</returns>
        public static VFolder CreateVFolder(DirectoryInfo directoryInfo)
        {
            // Use the new registry to get appropriate folder meta
            var folderMetaType = FolderMetaRegistry.GetFolderMetaType(directoryInfo);
            
            IFolderMeta folderMeta;
            if (folderMetaType != null)
            {
                folderMeta = (IFolderMeta)Activator.CreateInstance(folderMetaType, directoryInfo);
            }
            else
            {
                // Fallback to base folder
                folderMeta = new BaseFolder(directoryInfo);
            }

            var vFolder = new VFolder(folderMeta);
            return vFolder;
        }

        /// <summary>
        /// Create a VFile using the new FileMetaRegistry system
        /// </summary>
        /// <param name="fileInfo">File information</param>
        /// <returns>Configured VFile instance</returns>
        public static VFile CreateVFile(FileInfo fileInfo)
        {
            // Use the new registry to get appropriate file meta
            var extension = fileInfo.Extension;
            var fileMetaType = FileMetaRegistry.GetFileMetaTypeByExtension(extension);
            
            IFileMeta fileMeta;
            if (fileMetaType != null)
            {
                fileMeta = (IFileMeta)Activator.CreateInstance(fileMetaType, fileInfo);
            }
            else
            {
                // Fallback to common file
                fileMeta = new CommonFile(fileInfo);
            }

            var vFile = new VFile(fileMeta);
            return vFile;
        }

        /// <summary>
        /// Initialize registries - should be called during application startup
        /// </summary>
        public static void InitializeRegistries()
        {
            FolderMetaRegistry.RegisterFolderMetasFromAssemblies();
            FileMetaRegistry.RegisterFileMetasFromAssemblies();
        }

        /// <summary>
        /// Get all available folder meta types for debugging/inspection
        /// </summary>
        /// <returns>All registered folder meta types</returns>
        public static IEnumerable<Type> GetAvailableFolderMetaTypes()
        {
            return FolderMetaRegistry.GetAllFolderMetaTypes();
        }

        /// <summary>
        /// Get all available file meta types for debugging/inspection
        /// </summary>
        /// <returns>All registered file meta types</returns>
        public static IEnumerable<Type> GetAvailableFileMetaTypes()
        {
            return FileMetaRegistry.GetAllFileMetaTypes();
        }
    }
}