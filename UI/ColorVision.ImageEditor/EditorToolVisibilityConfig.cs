using ColorVision.Common.MVVM;
using System.Collections.Generic;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// Configuration for managing visibility of editor tools
    /// </summary>
    public class EditorToolVisibilityConfig : ViewModelBase, IImageEditorConfig
    {
        /// <summary>
        /// Dictionary to store visibility state for each tool by its GuidId
        /// </summary>
        public Dictionary<string, bool> ToolVisibility { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Get the visibility state for a specific tool
        /// </summary>
        /// <param name="guidId">The GuidId of the tool</param>
        /// <returns>True if visible, false if hidden. Default is true.</returns>
        public bool GetToolVisibility(string guidId)
        {
            if (string.IsNullOrEmpty(guidId))
                return true;

            if (ToolVisibility.TryGetValue(guidId, out bool isVisible))
                return isVisible;

            // Default to visible
            return true;
        }

        /// <summary>
        /// Set the visibility state for a specific tool
        /// </summary>
        /// <param name="guidId">The GuidId of the tool</param>
        /// <param name="isVisible">True to show, false to hide</param>
        public void SetToolVisibility(string guidId, bool isVisible)
        {
            if (string.IsNullOrEmpty(guidId))
                return;

            if (ToolVisibility.ContainsKey(guidId))
                ToolVisibility[guidId] = isVisible;
            else
                ToolVisibility.Add(guidId, isVisible);

            OnPropertyChanged(nameof(ToolVisibility));
        }
    }
}
