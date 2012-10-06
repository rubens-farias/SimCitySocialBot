using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Inspired.ClickThrough.Business
{
    class Mouse
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public static void Click(Point point, params MouseEvent[] flags)
        {
            SetCursorPos(point.X, point.Y);
            foreach (var flag in flags)
                mouse_event((int)flag, point.X, point.Y, 0, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        private static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            return lpPoint;
        }

        [Flags]
        public enum MouseEvent : uint
        {
            LeftDown    = 0x0002,
            LeftUp      = 0x0004,
            MiddleDown  = 0x0020,
            MiddleUp    = 0x0040,
            Move        = 0x0001,
            Absolute    = 0x8000,
            RightDown   = 0x0008,
            RightUp     = 0x0010,
            Wheel       = 0x0800,
            XDown       = 0x0080,
            XUp         = 0x0100
        }
    }
}
