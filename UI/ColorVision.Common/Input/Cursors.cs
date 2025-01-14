using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Resources;

namespace ColorVision.Input
{
    public static class Cursors
    {
        public static Cursor Eraser { get => EnsureCursor("eraser.cur"); }
        public static Cursor CursorPan { get => EnsureCursor("cursor_pan.cur"); }

        public static Cursor PickUp { get => EnsureCursor("pickup.cur"); }


        private static Dictionary<string, Cursor> _stockCursors = new Dictionary<string, Cursor>();

        internal static Cursor EnsureCursor(string cursorType)
        {
            if (_stockCursors.TryGetValue(cursorType, out Cursor cursor))
            {
                return cursor;
            }
            else
            {
                StreamResourceInfo stream = Application.GetResourceStream(new Uri($"/ColorVision.Common;component/assets/cursor/{cursorType}", UriKind.Relative));
                var cur = new Cursor(stream.Stream);
                _stockCursors.Add(cursorType, cur);
                return cur;
            }
        }
    }
}
