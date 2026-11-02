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
        private const uint LLKHF_EXTENDED = 0x20;

        private const int RightShiftDoubleTapMs = 420;
        private const int RightShiftMaxHoldMs = 350;

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
        private bool _rightShiftDown;
        private bool _rightShiftCandidate;
        private bool _rightShiftFired;
        private long _rightShiftStart;
        private long _lastRightShiftTap;

        public event EventHandler RightShiftDoublePressed;

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

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    HandleDown(vk, (data.flags & LLKHF_EXTENDED) != 0, (int)data.scanCode);
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    HandleUp(vk, (data.flags & LLKHF_EXTENDED) != 0, (int)data.scanCode);
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void HandleDown(int vk, bool extended, int scanCode)
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
                // Right Shift is a dedicated trigger: a quick double tap
                // starts the animation. It no longer toggles the IME, so the
                // two gestures cannot fight each other.
                if (IsRightShift(vk, extended, scanCode))
                {
                    HandleRightShiftDown();
                    return;
                }

                if (!_shiftDown)
                {
                    _shiftDown = true;
                    _shiftCandidate = true;
                    _shiftStart = AppClock.Milliseconds;
                }
                return;
            }

            if (_shiftDown) _shiftCandidate = false;
            if (_rightShiftDown) _rightShiftCandidate = false;
            _lastRightShiftTap = 0;

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

        private void HandleUp(int vk, bool extended, int scanCode)
        {
            if (vk == VK_CAPITAL)
            {
                _capsDown = false;
                return;
            }

            if (vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT)
            {
                if (IsRightShift(vk, extended, scanCode))
                {
                    HandleRightShiftUp();
                    return;
                }

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

        private static bool IsRightShift(int vk, bool extended, int scanCode)
        {
            if (vk == VK_RSHIFT) return true;
            // Some layouts report the generic VK_SHIFT; the extended flag or
            // the right-Shift scan code still identifies the physical key.
            return vk == VK_SHIFT && (extended || scanCode == 0x36);
        }

        private void HandleRightShiftDown()
        {
            if (_rightShiftDown) return;

            long now = AppClock.Milliseconds;
            _rightShiftDown = true;
            _rightShiftCandidate = true;
            _rightShiftFired = false;
            _rightShiftStart = now;

            if (_lastRightShiftTap != 0
                && now - _lastRightShiftTap <= RightShiftDoubleTapMs
                && MouseHook.LastButtonDownMs <= _lastRightShiftTap)
            {
                _lastRightShiftTap = 0;
                _rightShiftFired = true;
                EventHandler handler = RightShiftDoublePressed;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            else
            {
                // Too slow, or a mouse click broke the sequence.
                _lastRightShiftTap = 0;
            }
        }

        private void HandleRightShiftUp()
        {
            if (!_rightShiftDown) return;
            _rightShiftDown = false;

            long now = AppClock.Milliseconds;
            bool quick = now - _rightShiftStart <= RightShiftMaxHoldMs;
            if (_rightShiftCandidate && quick && !_rightShiftFired
                && MouseHook.LastButtonDownMs <= _rightShiftStart)
            {
                // Remember only a clean tap; the next tap has to land inside
                // the double-tap window to fire.
                _lastRightShiftTap = now;
            }
            else
            {
                _lastRightShiftTap = 0;
            }

            _rightShiftCandidate = false;
            _rightShiftFired = false;
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
