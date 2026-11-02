using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FindMyCursor
{
    /// <summary>
    /// Owns the pointer colour for both the normal arrow (OCR_NORMAL) and the
    /// text I-beam (OCR_IBEAM):
    ///   red   = Chinese
    ///   blue  = English
    ///   green = Caps Lock (uppercase)
    /// The Chinese/English baseline is calibrated once from the Windows 11
    /// text-input host's global conversion mode. After that, a standalone
    /// Shift / Ctrl+Space toggles English; Shift+click and Shift+key do not
    /// toggle because they are selection/shortcut gestures.
    /// </summary>
    internal sealed class ImeIndicator : IDisposable
    {
        private const int VkCapital = 0x14;

        private static readonly Color ChineseColor = Color.FromArgb(255, 0xE6, 0x37, 0x41);
        private static readonly Color UpperColor = Color.FromArgb(255, 0x14, 0xB4, 0x5F);
        private static readonly Color EnglishColor = Color.FromArgb(255, 0x2D, 0x73, 0xF5);
        private static IntPtr _tiaIme;
        private static long _tiaRefresh;
        private static int _lastGlobalConversion = -1;
        private static bool _globalImeOk;
        private static bool _englishInitialized;

        // Shift/Ctrl+Space toggle inside the Chinese layout. The baseline is
        // calibrated once from the global input indicator, then Shift keeps it
        // in sync without guessing from scratch.
        public static bool LocalEnglish;

        private readonly Timer _timer;
        private readonly IntPtr _baseArrow;
        private readonly IntPtr _baseIBeam;
        private bool _enabled = true;
        private int _currentState = -1;
        private int _pendingState = -1;
        private long _pendingSince;

        public static bool CapsOn;

        public ImeIndicator()
        {
            IntPtr live = Native.LoadCursor(IntPtr.Zero, (IntPtr)Native.IDC_ARROW);
            if (live != IntPtr.Zero) _baseArrow = Native.CopyIcon(live);

            IntPtr liveBeam = Native.LoadCursor(IntPtr.Zero, (IntPtr)Native.IDC_IBEAM);
            if (liveBeam != IntPtr.Zero) _baseIBeam = Native.CopyIcon(liveBeam);

            CapsOn = (Native.GetKeyState(VkCapital) & 1) != 0;

            _timer = new Timer();
            _timer.Interval = 150;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (!value)
                {
                    RestoreBase();
                    _currentState = -1;
                }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_enabled || CursorScaler.IsActive)
            {
                _pendingState = -1;
                return;
            }

            int state = GetState();
            if (state == _currentState)
            {
                _pendingState = -1;
                return;
            }

            // Debounce: switching foreground to a short-lived tooltip / agent
            // panel with an English layout used to flash the cursor blue for
            // one tick. Require the new state to stay stable before applying.
            long now = AppClock.Milliseconds;
            if (state != _pendingState)
            {
                _pendingState = state;
                _pendingSince = now;
                return;
            }
            if (now - _pendingSince < 400) return;

            if (SetStateCursor(state))
            {
                _currentState = state;
                _pendingState = -1;
                Log.Write("ime state " + state + " tia=" +
                    (_globalImeOk ? _lastGlobalConversion.ToString() : "fallback"));
            }
        }

        /// <summary>0 = English, 1 = Chinese, 2 = Caps Lock.</summary>
        private int GetState()
        {
            if (CapsOn) return 2;

            int language = GetLayoutLanguage();
            if (!IsChinese(language))
            {
                LocalEnglish = false;
                return 0;
            }

            // The Windows 11 text-input host is used once to calibrate the
            // baseline Chinese/English state. After that Shift / Ctrl+Space
            // keeps it in sync; do not send a cross-process message every
            // 150 ms once the baseline is known.
            if (!_englishInitialized)
            {
                bool native;
                if (TryReadGlobalIme(out native))
                {
                    LocalEnglish = !native;
                    _englishInitialized = true;
                    Log.Write("ime baseline english=" + LocalEnglish + " tia=" + _lastGlobalConversion);
                }
            }

            return LocalEnglish ? 0 : 1;
        }

        public static void ToggleLocalEnglish()
        {
            LocalEnglish = !LocalEnglish;
            _englishInitialized = true;
            Log.Write("ime local english=" + LocalEnglish);
        }

        public static void SetChinese()
        {
            LocalEnglish = false;
            _englishInitialized = true;
            Log.Write("ime manual chinese");
        }

        public static void SetEnglish()
        {
            LocalEnglish = true;
            _englishInitialized = true;
            Log.Write("ime manual english");
        }

        public void ApplyStateNow()
        {
            if (!_enabled || CursorScaler.IsActive) return;

            int state = GetState();
            if (SetStateCursor(state))
            {
                _currentState = state;
                _pendingState = -1;
                Log.Write("ime manual state " + state);
            }
        }

        private bool TryReadGlobalIme(out bool native)
        {
            native = false;
            _globalImeOk = false;

            IntPtr ime = GetTextInputHostImeWindow();
            if (ime == IntPtr.Zero) return false;

            IntPtr result;
            IntPtr sent = Native.SendMessageTimeout(ime, Native.WM_IME_CONTROL,
                (IntPtr)Native.IMC_GETCONVERSIONMODE, IntPtr.Zero,
                Native.SMTO_ABORTIFHUNG, 120, out result);
            if (sent == IntPtr.Zero) return false;

            int conversion = result.ToInt32();
            if (conversion < 0 || conversion > 0xFFFF) return false;

            _lastGlobalConversion = conversion;
            _globalImeOk = true;
            native = (conversion & 0x0001) != 0;
            return true;
        }

        private bool TryReadForegroundImeOpen(out bool open)
        {
            open = false;

            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;

            IntPtr ime = Native.ImmGetDefaultIMEWnd(foreground);
            if (ime == IntPtr.Zero) return false;

            IntPtr result;
            IntPtr sent = Native.SendMessageTimeout(ime, Native.WM_IME_CONTROL,
                (IntPtr)Native.IMC_GETOPENSTATUS, IntPtr.Zero,
                Native.SMTO_ABORTIFHUNG, 120, out result);
            if (sent == IntPtr.Zero) return false;

            open = result != IntPtr.Zero;
            return true;
        }

        private static IntPtr GetTextInputHostImeWindow()
        {
            if (_tiaIme != IntPtr.Zero && Native.IsWindow(_tiaIme)) return _tiaIme;

            long now = AppClock.Milliseconds;
            if (now - _tiaRefresh < 2000) return IntPtr.Zero;
            _tiaRefresh = now;

            IntPtr host = IntPtr.Zero;
            uint hostPid = 0;

            // FindWindow can return a hidden CoreWindow from another process
            // with the same title; enumerate and prefer the visible text-input
            // host instead.
            Native.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                StringBuilder cls = new StringBuilder(128);
                Native.GetClassName(hWnd, cls, cls.Capacity);
                if (!string.Equals(cls.ToString(), "Windows.UI.Core.CoreWindow",
                    StringComparison.OrdinalIgnoreCase)) return true;

                StringBuilder title = new StringBuilder(256);
                Native.GetWindowText(hWnd, title, title.Capacity);
                if (title.ToString().IndexOf("Text Input", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
                if (!Native.IsWindowVisible(hWnd)) return true;

                host = hWnd;
                uint pid;
                Native.GetWindowThreadProcessId(hWnd, out pid);
                hostPid = pid;
                return false;
            }, IntPtr.Zero);

            if (hostPid == 0) return IntPtr.Zero;

            // The TIA's hidden "IME" window is a top-level window owned by the
            // same process, not a child of the CoreWindow.
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint pid;
                Native.GetWindowThreadProcessId(hWnd, out pid);
                if (pid != hostPid) return true;

                StringBuilder name = new StringBuilder(64);
                Native.GetClassName(hWnd, name, name.Capacity);
                if (string.Equals(name.ToString(), "IME", StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            _tiaIme = found;
            return found;
        }

        public static void ToggleCaps()
        {
            CapsOn = !CapsOn;
            Log.Write("ime caps=" + CapsOn);
        }

        /// <summary>
        /// Legacy IMM probe, kept for diagnostics only. Not called by GetState:
        /// Chrome/Electron return stale IMM conversion modes, which is exactly
        /// what made Chinese look blue. GET commands only; never add SET ones.
        /// </summary>
        private bool TryReadIme(out bool native)
        {
            native = false;

            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;

            uint pid;
            uint threadId = Native.GetWindowThreadProcessId(foreground, out pid);

            IntPtr focus = IntPtr.Zero;
            Native.GUITHREADINFO threadInfo = new Native.GUITHREADINFO();
            threadInfo.cbSize = Marshal.SizeOf(typeof(Native.GUITHREADINFO));
            if (Native.GetGUIThreadInfo(threadId, ref threadInfo)) focus = threadInfo.hwndFocus;

            IntPtr imeWnd = Native.ImmGetDefaultIMEWnd(foreground);
            IntPtr contextWindow = IntPtr.Zero;
            IntPtr himc = IntPtr.Zero;

            if (focus != IntPtr.Zero)
            {
                himc = Native.ImmGetContext(focus);
                if (himc != IntPtr.Zero) contextWindow = focus;
            }
            if (himc == IntPtr.Zero)
            {
                himc = Native.ImmGetContext(foreground);
                if (himc != IntPtr.Zero) contextWindow = foreground;
            }
            if (himc == IntPtr.Zero && imeWnd != IntPtr.Zero)
            {
                himc = Native.ImmGetContext(imeWnd);
                if (himc != IntPtr.Zero) contextWindow = imeWnd;
            }

            if (himc != IntPtr.Zero)
            {
                try
                {
                    bool open = Native.ImmGetOpenStatus(himc);
                    if (!open) return true; // valid context says English

                    int conversion;
                    int sentence;
                    if (Native.ImmGetConversionStatus(himc, out conversion, out sentence))
                    {
                        native = (conversion & 0x0001) != 0;
                        return true;
                    }
                    return false;
                }
                finally
                {
                    Native.ImmReleaseContext(contextWindow, himc);
                }
            }

            if (imeWnd != IntPtr.Zero)
            {
                IntPtr result;
                IntPtr sent = Native.SendMessageTimeout(imeWnd, Native.WM_IME_CONTROL,
                    (IntPtr)Native.IMC_GETOPENSTATUS, IntPtr.Zero,
                    Native.SMTO_ABORTIFHUNG, 120, out result);
                if (sent != IntPtr.Zero)
                {
                    // A successful reply of 0 means the IME is closed
                    // (English / direct input). The old code treated 0 as
                    // "no answer" and then fell through to the Chinese
                    // layout, which is why English could show red.
                    if (result == IntPtr.Zero)
                    {
                        native = false;
                        return true;
                    }

                    IntPtr conversionResult;
                    IntPtr sentConversion = Native.SendMessageTimeout(imeWnd, Native.WM_IME_CONTROL,
                        (IntPtr)Native.IMC_GETCONVERSIONMODE, IntPtr.Zero,
                        Native.SMTO_ABORTIFHUNG, 120, out conversionResult);
                    if (sentConversion != IntPtr.Zero)
                    {
                        int conversion = conversionResult.ToInt32();
                        // Chrome/Electron return 0 or 1 here; ignore garbage.
                        if (conversion >= 0 && conversion <= 0xFFFF)
                        {
                            native = (conversion & 0x0001) != 0;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private int GetLayoutLanguage()
        {
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return 0;

            uint pid;
            uint threadId = Native.GetWindowThreadProcessId(foreground, out pid);
            IntPtr layout = Native.GetKeyboardLayout(threadId);
            return ((int)(long)layout) & 0xFFFF;
        }

        private static bool IsChinese(int language)
        {
            return language == 0x0804 || language == 0x0404 || language == 0x0C04
                || language == 0x1004 || language == 0x1404;
        }

        private bool SetStateCursor(int state)
        {
            Color color = state == 1 ? ChineseColor : (state == 2 ? UpperColor : EnglishColor);
            int size = Math.Max(16, Math.Min(64, Settings.ImeSize));
            int hotX;
            int hotY;
            int beamX;
            int beamY;
            bool applied = false;

            using (Bitmap arrow = CursorArt.CreateSignatureArrow(size, color,
                Settings.OutlineColor, out hotX, out hotY))
            using (Bitmap beam = CursorArt.CreateSignatureIBeam(size, color,
                out beamX, out beamY))
            {
                IntPtr hArrow = CursorArt.ToHandle(arrow, hotX, hotY, false);
                IntPtr hBeam = CursorArt.ToHandle(beam, beamX, beamY, false);

                if (hArrow != IntPtr.Zero)
                {
                    if (Native.SetSystemCursor(hArrow, Native.OCR_NORMAL))
                    {
                        hArrow = IntPtr.Zero; // ownership passed to the system
                        applied = true;
                    }
                    else
                    {
                        Native.DestroyCursor(hArrow);
                    }
                }

                if (hBeam != IntPtr.Zero)
                {
                    if (Native.SetSystemCursor(hBeam, Native.OCR_IBEAM))
                    {
                        hBeam = IntPtr.Zero; // ownership passed to the system
                        applied = true;
                    }
                    else
                    {
                        Native.DestroyCursor(hBeam);
                    }
                }
            }

            return applied;
        }

        private void RestoreBase()
        {
            // Reload the user's cursor scheme so both the arrow and the text
            // I-beam go back to their configured shapes/colours.
            if (Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0)) return;

            if (_baseArrow != IntPtr.Zero)
            {
                IntPtr copy = Native.CopyIcon(_baseArrow);
                if (copy != IntPtr.Zero)
                {
                    if (!Native.SetSystemCursor(copy, Native.OCR_NORMAL)) Native.DestroyCursor(copy);
                }
            }

            if (_baseIBeam != IntPtr.Zero)
            {
                IntPtr copy = Native.CopyIcon(_baseIBeam);
                if (copy != IntPtr.Zero)
                {
                    if (!Native.SetSystemCursor(copy, Native.OCR_IBEAM)) Native.DestroyCursor(copy);
                }
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            RestoreBase();

            if (_baseArrow != IntPtr.Zero) Native.DestroyCursor(_baseArrow);
            if (_baseIBeam != IntPtr.Zero) Native.DestroyCursor(_baseIBeam);
        }
    }
}
