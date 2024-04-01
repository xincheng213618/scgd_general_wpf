using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Common.NativeMethods
{
    public static class ShortcutCreator
    {
        public static void CreateShortcut(string shortcutName, string shortcutPath, string targetFileLocation)
        {
            // Create a new WshShell object to access WSH functionality
            WshShell shell = new WshShell();

            // Create the shortcut object
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath + "\\" + shortcutName + ".lnk");

            // Set the properties of the shortcut
            shortcut.Description = "ColorVision"; // The description of the shortcut
            shortcut.IconLocation = targetFileLocation + ",0"; // The icon of the shortcut
            shortcut.TargetPath = targetFileLocation; // The path of the file that will launch when the shortcut is run
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetFileLocation); // The working directory for the shortcut
            shortcut.WindowStyle = 1; // Normal window
            shortcut.Arguments = ""; // No additional arguments

            // Save the shortcut
            shortcut.Save();
        }
    }
}
    