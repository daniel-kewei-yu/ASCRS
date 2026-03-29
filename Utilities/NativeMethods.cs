// ============================================================================
// File:        NativeMethods.cs
// Project:     Aperture Science Cursor Relocation Suite (ASCRS)
// Author:      Daniel Yu
// Date:        2026-03-27
// Description:
//   Contains P/Invoke declarations for Windows API functions used by the
//   application. This includes functions for:
//     - Manipulating window styles (WS_EX_TRANSPARENT, WS_EX_APPWINDOW)
//     - Setting and moving windows
//     - Getting the cursor position and the window under it
//     - Posting messages to other windows (for click‑through)
//     - Registering and unregistering global hotkeys
//   Also defines several constants and structures used by these functions.
// ============================================================================

using System;
using System.Runtime.InteropServices;

namespace ASCRS.Utilities
{
    /// <summary>
    /// Provides static methods that wrap Windows API calls.
    /// </summary>
    public static class NativeMethods
    {
        // --------------------------------------------------------------------
        // Window style constants
        // --------------------------------------------------------------------

        /// <summary>Index used with GetWindowLong/SetWindowLong to retrieve the extended window style.</summary>
        public const int GWL_EXSTYLE = -20;

        /// <summary>Extended window style: the window is transparent to mouse clicks.</summary>
        public const int WS_EX_TRANSPARENT = 0x20;

        // --------------------------------------------------------------------
        // SetWindowPos flags
        // --------------------------------------------------------------------

        public const uint SWP_NOMOVE = 0x0002;      // Do not change the window's position
        public const uint SWP_NOSIZE = 0x0001;      // Do not change the window's size
        public const uint SWP_NOZORDER = 0x0004;    // Do not change the Z order
        public const uint SWP_FRAMECHANGED = 0x0020; // Force a frame change

        // --------------------------------------------------------------------
        // Mouse message constants (used for forwarding clicks)
        // --------------------------------------------------------------------

        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;

        // --------------------------------------------------------------------
        // Cursor resource identifier (standard arrow)
        // --------------------------------------------------------------------

        public const uint OCR_NORMAL = 32512;

        // --------------------------------------------------------------------
        // Structures
        // --------------------------------------------------------------------

        /// <summary>
        /// Represents a point (x, y) in screen coordinates.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y) { X = x; Y = y; }
        }

        /// <summary>
        /// Holds information about the current system cursor.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;          // Size of the structure (must be set before calling GetCursorInfo)
            public int flags;           // Cursor flags (not used in our code)
            public IntPtr hCursor;      // Handle to the cursor
            public POINT ptScreenPos;   // Current screen position of the cursor
        }

        // --------------------------------------------------------------------
        // P/Invoke declarations
        // --------------------------------------------------------------------

        /// <summary>Retrieves the current cursor information.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        /// <summary>Loads a cursor resource from the system or a file.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        /// <summary>Sets the system cursor globally (affects all windows).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetSystemCursor(IntPtr hcur, uint id);

        /// <summary>Destroys a cursor handle created by LoadCursor or LoadImage.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyCursor(IntPtr hCursor);

        /// <summary>Retrieves the current window style (or extended style).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        /// <summary>Changes the window style (or extended style).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>Changes the size, position, and Z order of a window.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        /// <summary>Retrieves the current cursor position (screen coordinates).</summary>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>Returns a handle to the window that contains the given point.</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        /// <summary>Places a message in the message queue of the specified window.</summary>
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Registers a global hotkey that works even when the window does not have focus.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>Unregisters a previously registered global hotkey.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}