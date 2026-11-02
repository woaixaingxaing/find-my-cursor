using System;
using System.Runtime.InteropServices;

namespace FindMyCursor
{
    /// <summary>
    /// Tracks Caps Lock and standalone Shift / Ctrl+Space toggles. A Shift tap
    /// only counts when no other key and no mouse button was involved, so
    /// Shift+click text selection does not flip the pointer colour.
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_SHIFT = 0x10;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_SPACE = 0x20;
        private const int VK_CAPITAL = 0x14;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hook;
        private bool _shiftDown;
        private bool _shiftCandidate;
        private long _shiftStart;
        private bool _ctrlDown;
        private bool _capsDown;

        public KeyboardHook()
        {
            _proc = Callback;
        }

        public bool Start()
        {
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
            return _hook != IntPtr.Zero;
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                int vk = (int)data.vkCode;
                long msg = (long)wParam;

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) HandleDown(vk);
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) HandleUp(vk);
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void HandleDown(int vk)
        {
            if (vk == VK_CAPITAL)
            {
                if (!_capsDown)
                {
                    _capsDown = true;
                    ImeIndicator.ToggleCaps();
                }
                return;
            }

            if (vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT)
            {
                if (!_shiftDown)
                {
                    _shiftDown = true;
                    _shiftCandidate = true;
                    _shiftStart = AppClock.Milliseconds;
                }
                return;
            }

            if (_shiftDown) _shiftCandidate = false;

            if (vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL)
            {
                _ctrlDown = true;
                return;
            }

            if (vk == VK_SPACE && _ctrlDown)
            {
                _shiftCandidate = false;
                ImeIndicator.ToggleLocalEnglish();
            }
        }

        private void HandleUp(int vk)
        {
            if (vk == VK_CAPITAL)
            {
                _capsDown = false;
                return;
            }

            if (vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT)
            {
                long now = AppClock.Milliseconds;
                bool quick = now - _shiftStart < 600;
                bool mouseDuringShift = MouseHook.LastButtonDownMs >= _shiftStart;
                if (_shiftDown && _shiftCandidate && quick && !mouseDuringShift)
                {
                    ImeIndicator.ToggleLocalEnglish();
                }
                _shiftDown = false;
                _shiftCandidate = false;
                return;
            }

            if (vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL)
            {
                _ctrlDown = false;
            }
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
