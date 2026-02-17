using System;
using System.Runtime.InteropServices;

namespace ToolTopBar
{
    internal static class NativeMethods
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static void HideFromAltTab(IntPtr hwnd)
        {
            var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            var style = (IntPtr)((exStyle.ToInt64() | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, style);
        }

        // AppBar interop
        public const int ABM_NEW = 0x00000000;
        public const int ABM_REMOVE = 0x00000001;
        public const int ABM_QUERYPOS = 0x00000002;
        public const int ABM_SETPOS = 0x00000003;
        public const int ABM_WINDOWPOSCHANGED = 0x0000009;

        public const int ABN_STATECHANGE = 0x0000000;
        public const int ABN_POSCHANGED = 0x0000001;
        public const int ABN_FULLSCREENAPP = 0x0000002;
        public const int ABN_WINDOWARRANGE = 0x0000003;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint RegisterWindowMessage(string lpString);
        public const int ABE_TOP = 1;
        public const int ABE_LEFT = 0;
        public const int ABE_RIGHT = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
        // Keyboard input (SendInput) to support Win+Ctrl shortcuts.
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_LEFT = 0x25;
        private const ushort VK_RIGHT = 0x27;
        private const ushort VK_D = 0x44;
        private const ushort VK_F4 = 0x73;
        private const ushort VK_LWIN = 0x5B;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;

            // Included to ensure the union has the correct size on x64.
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private static bool IsExtendedKey(ushort vk) => vk is VK_LEFT or VK_RIGHT or VK_LWIN;

        private static INPUT KeyDown(ushort vk) => new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0
                }
            }
        };

        private static INPUT KeyUp(ushort vk) => new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = (IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0) | KEYEVENTF_KEYUP
                }
            }
        };

        private static void SendKeyChord(ushort modifier1, ushort modifier2, ushort mainKey)
        {
            var inputs = new[]
            {
                KeyDown(modifier1),
                KeyDown(modifier2),
                KeyDown(mainKey),
                KeyUp(mainKey),
                KeyUp(modifier2),
                KeyUp(modifier1)
            };

            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length)
            {
                throw new InvalidOperationException($"SendInput falló. Enviados={sent}/{inputs.Length}. Win32Error={Marshal.GetLastWin32Error()}");
            }
        }

        public static void SwitchVirtualDesktopLeft() => SendKeyChord(VK_LWIN, VK_CONTROL, VK_LEFT);
        public static void SwitchVirtualDesktopRight() => SendKeyChord(VK_LWIN, VK_CONTROL, VK_RIGHT);
        public static void CreateVirtualDesktop() => SendKeyChord(VK_LWIN, VK_CONTROL, VK_D);
        public static void CloseVirtualDesktop() => SendKeyChord(VK_LWIN, VK_CONTROL, VK_F4);

        // SHGetFileInfo helper to retrieve system icons (e.g. folder icon)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        // Return raw hIcon via SHGetFileInfo (caller responsible for destroying)
        public static IntPtr GetSystemIconHandle(string path, bool forDirectory, bool smallIcon)
        {
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (smallIcon ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            uint attrs = forDirectory ? FILE_ATTRIBUTE_DIRECTORY : 0u;
            var res = SHGetFileInfo(path ?? "", attrs, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            return shfi.hIcon;
        }
    }
}
