using System;

namespace ColorVision.UI.Menus
{
    /// <summary>
    /// Marks a class as a menu item that will be automatically discovered and shown in
    /// the application menu. Supports lazy loading — the class is only instantiated when
    /// the user actually clicks the menu item.
    /// <para>
    /// This attribute-based mechanism is an alternative to implementing <see cref="IMenuItem"/>
    /// or inheriting <see cref="MenuItemBase"/>. Both discovery paths are fully supported
    /// side-by-side to allow safe, gradual migration.
    /// </para>
    /// <para>
    /// The decorated class must expose an <c>Execute()</c> method (or implement
    /// <see cref="IMenuItem"/>). The menu UI shell is built entirely from the attribute
    /// metadata; the class itself is not instantiated until the user clicks the item.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MenuItemAttribute : Attribute
    {
        /// <summary>
        /// The name of the window target this menu item belongs to
        /// (e.g., <see cref="MenuItemConstants.MainWindowTarget"/> or
        /// <see cref="MenuItemConstants.GlobalTarget"/>).
        /// Defaults to <see cref="MenuItemConstants.GlobalTarget"/>.
        /// </summary>
        public string TargetName { get; set; } = MenuItemConstants.GlobalTarget;

        /// <summary>
        /// The <see cref="IMenuItem.GuidId"/> of the parent menu item under which
        /// this item will appear.  Use <see cref="MenuItemConstants.Menu"/> to place
        /// the item at the root menu bar level.
        /// </summary>
        public string OwnerGuid { get; set; } = MenuItemConstants.Menu;

        /// <summary>
        /// A stable, unique identifier for this menu item.
        /// When <c>null</c> the class name is used as the identifier.
        /// </summary>
        public string? GuidId { get; set; }

        /// <summary>
        /// Determines the display order relative to sibling menu items.
        /// Smaller values appear first.
        /// </summary>
        public int Order { get; set; } = 1;

        /// <summary>
        /// The display text shown in the menu (the menu item header).
        /// </summary>
        public string? Header { get; set; }

        /// <summary>
        /// Optional keyboard gesture hint displayed on the right side of the menu item
        /// (e.g., <c>"Ctrl+O"</c>).  This is display-only; actual hotkey registration
        /// must be done separately via the hotkey system.
        /// </summary>
        public string? InputGestureText { get; set; }

        /// <summary>Initialises the attribute without setting a header.</summary>
        public MenuItemAttribute() { }

        /// <summary>Initialises the attribute and sets the menu header text.</summary>
        /// <param name="header">The display text shown in the menu.</param>
        public MenuItemAttribute(string header)
        {
            Header = header;
        }
    }
}
