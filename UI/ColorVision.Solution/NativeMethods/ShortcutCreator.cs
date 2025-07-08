
using IWshRuntimeLibrary;
using System.IO;

namespace ColorVision.Common.NativeMethods
{
    public static class ShortcutCreator
    {
        public static void CreateShortcut(string shortcutName, string shortcutPath, string targetFileLocation,string arguments ="")
        {
            // Create a new WshShell object to access WSH functionality
            WshShell shell = new();

            // Create the shortcut object
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath + "\\" + shortcutName + ".lnk");

            // Set the properties of the shortcut
            shortcut.Description = "ColorVision"; // The description of the shortcut
            shortcut.IconLocation = targetFileLocation + ",0"; // The icon of the shortcut
            shortcut.TargetPath = targetFileLocation; // The path of the file that will launch when the shortcut is run
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetFileLocation); // The working directory for the shortcut
            shortcut.WindowStyle = 1; // Normal window
            shortcut.Arguments = arguments; // No additional arguments

            // Save the shortcut
            shortcut.Save();
        }
        public static string GetShortcutTargetFile(string shortcutFilename)
        {
             WshShell shell = new WshShell();
            IWshShortcut link = (IWshShortcut)shell.CreateShortcut(shortcutFilename); // Load the shortcut

            // Return the target path
            return link.TargetPath;
        }
    }
}
    