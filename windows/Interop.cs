using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FindMyCursor
{
    internal static class AppClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static long Milliseconds
        {
            get { return Watch.ElapsedMilliseconds; }
        }
    }

    internal static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_HOTKEY = 0x0312;

        public const uint OCR_NORMAL = 32512;
        public const uint OCR_IBEAM = 32513;
        public const uint IDC_ARROW = 32512;
        public const uint IDC_IBEAM = 32513;
        public const uint SPI_SETCURSORS = 0x0057;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int w, int h) { cx = w; cy = h; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public short bmPlanes;
            public short bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [DllImport("user32.dll")]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hwnd);

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetContext(IntPtr hwnd);

        [DllImport("imm32.dll")]
        public static extern bool ImmGetOpenStatus(IntPtr himc);

        [DllImport("imm32.dll")]
        public static extern bool ImmGetConversionStatus(IntPtr himc, out int conversion, out int sentence);

        [DllImport("imm32.dll")]
        public static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr himc);

        public const uint WM_IME_CONTROL = 0x0283;
        // GET commands only. Never send 0x0002/0x0006 here: 0x0002 is
        // IMC_SETCONVERSIONMODE and would silently switch the IME to English.
        public const int IMC_GETCONVERSIONMODE = 0x0001;
        public const int IMC_GETOPENSTATUS = 0x0005;
        public const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
            IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType,
            int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO pii);

        [DllImport("gdi32.dll")]
        public static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

        [DllImport("gdi32.dll")]
        public static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[] bits, ref BITMAPINFO bmi, uint usage);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int value);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, IntPtr lpBits);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyCursor(IntPtr hCursor);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
            int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        public static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int mode);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);
    }

    internal sealed class MouseHook : IDisposable
    {
        private readonly Native.LowLevelMouseProc _proc;
        private IntPtr _hook;

        public static long LastButtonDownMs;

        public event Action<int, int> MouseMoved;

        public MouseHook()
        {
            _proc = Callback;
        }

        public bool Start()
        {
            IntPtr hMod = Native.GetModuleHandle(null);
            _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, hMod, 0);
            return _hook != IntPtr.Zero;
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                long message = (long)wParam;
                if (message == Native.WM_MOUSEMOVE)
                {
                    Native.MSLLHOOKSTRUCT data = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    Action<int, int> handler = MouseMoved;
                    if (handler != null) handler(data.pt.X, data.pt.Y);
                }
                else if (message == Native.WM_LBUTTONDOWN || message == Native.WM_RBUTTONDOWN
                    || message == Native.WM_MBUTTONDOWN || message == Native.WM_XBUTTONDOWN)
                {
                    LastButtonDownMs = AppClock.Milliseconds;
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int HotkeyId = 0xBEEF;
        private bool _registered;

        public event EventHandler Pressed;

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "FindMyCursor.Hotkey";
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
            _registered = Native.RegisterHotKey(Handle, HotkeyId,
                Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.F);
        }

        public bool Registered
        {
            get { return _registered; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                EventHandler handler = Pressed;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            IntPtr handle = Handle;
            if (handle != IntPtr.Zero)
            {
                if (_registered) Native.UnregisterHotKey(handle, HotkeyId);
                DestroyHandle();
            }
        }
    }

    internal static class CursorArt
    {
        private static readonly PointF[] ArrowPoints = new PointF[]
        {
            new PointF(2.6f, 1.6f),
            new PointF(2.6f, 27.6f),
            new PointF(9.1f, 21.2f),
            new PointF(13.5f, 31.7f),
            new PointF(17.7f, 29.9f),
            new PointF(13.3f, 19.6f),
            new PointF(22.6f, 19.6f)
        };

        public static Bitmap CreateArrow(int height, out int hotX, out int hotY)
        {
            float s = height / 36f;
            int pad = Math.Max(4, (int)Math.Round(7 * s));
            int width = (int)Math.Round(26 * s) + pad * 2;
            int bmpHeight = (int)Math.Round(36 * s) + pad * 2;

            Bitmap bmp = new Bitmap(width, bmpHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                PointF[] pts = new PointF[ArrowPoints.Length];
                for (int i = 0; i < ArrowPoints.Length; i++)
                {
                    pts[i].X = pad + ArrowPoints[i].X * s;
                    pts[i].Y = pad + ArrowPoints[i].Y * s;
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(pts);

                    using (GraphicsPath shadowPath = (GraphicsPath)path.Clone())
                    using (Matrix offset = new Matrix())
                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(85, 0, 0, 0)))
                    {
                        offset.Translate(0, Math.Max(1f, 2.4f * s));
                        shadowPath.Transform(offset);
                        g.FillPath(shadow, shadowPath);
                    }

                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(255, 251, 251, 253)))
                    {
                        g.FillPath(fill, path);
                    }

                    float stroke = Math.Max(1.25f, 1.55f * s);
                    using (Pen pen = new Pen(Color.FromArgb(255, 21, 22, 28), stroke))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, path);
                    }
                }
            }

            hotX = pad + (int)Math.Round(2.6f * s);
            hotY = pad + (int)Math.Round(1.6f * s);
            return bmp;
        }

        /// <summary>
        /// A high-resolution arrow in the cursor's own colours. Used instead of
        /// enlarging a 20 px bitmap, which always ends up blurry or blocky.
        /// </summary>
        public static Bitmap CreateStyledArrow(int height, Color fill, Color outline,
            out int hotX, out int hotY)
        {
            // The visible arrow spans 30.1 units of the 36-unit path; scale by
            // the visible height so 96 / 128 / 160 px mean what they say.
            float s = height / 30.1f;
            int pad = Math.Max(4, (int)Math.Round(5 * s));
            int width = (int)Math.Round(26 * s) + pad * 2;
            int bmpHeight = (int)Math.Round(36 * s) + pad * 2;

            Bitmap bmp = new Bitmap(width, bmpHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                PointF[] pts = new PointF[ArrowPoints.Length];
                for (int i = 0; i < ArrowPoints.Length; i++)
                {
                    pts[i].X = pad + ArrowPoints[i].X * s;
                    pts[i].Y = pad + ArrowPoints[i].Y * s;
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(pts);

                    using (GraphicsPath shadowPath = (GraphicsPath)path.Clone())
                    using (Matrix offset = new Matrix())
                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
                    {
                        offset.Translate(0, Math.Max(1f, 1.5f * s));
                        shadowPath.Transform(offset);
                        g.FillPath(shadow, shadowPath);
                    }

                    using (SolidBrush fillBrush = new SolidBrush(fill))
                    {
                        g.FillPath(fillBrush, path);
                    }

                    float stroke = Math.Max(1.4f, height * 0.022f);
                    using (Pen pen = new Pen(outline, stroke))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, path);
                    }
                }
            }

            hotX = pad + (int)Math.Round(2.6f * s);
            hotY = pad + (int)Math.Round(1.6f * s);
            return bmp;
        }

        /// <summary>
        /// Our own enlarged pointer: red gradient fill, dark outline, soft
        /// shadow and a small specular highlight. Same colour family as the
        /// user's IME cursor, but drawn as a clean high-resolution arrow.
        /// </summary>
        public static Bitmap CreateSignatureArrow(int height, Color fill, Color outline,
            out int hotX, out int hotY)
        {
            float s = height / 30.1f;
            int pad = Math.Max(8, (int)Math.Round(8 * s));
            int width = (int)Math.Round(26 * s) + pad * 2;
            int bmpHeight = (int)Math.Round(36 * s) + pad * 2;

            Bitmap bmp = new Bitmap(width, bmpHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                PointF[] pts = new PointF[ArrowPoints.Length];
                for (int i = 0; i < ArrowPoints.Length; i++)
                {
                    pts[i].X = pad + ArrowPoints[i].X * s;
                    pts[i].Y = pad + ArrowPoints[i].Y * s;
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(pts);

                    RectangleF bounds = path.GetBounds();
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new PointF(bounds.Left, bounds.Top),
                        new PointF(bounds.Left, bounds.Bottom),
                        Lighten(fill, 0.16f),
                        Darken(fill, 0.12f)))
                    {
                        g.FillPath(brush, path);
                    }

                    using (Pen highlight = new Pen(Color.FromArgb(70, 255, 255, 255),
                        Math.Max(1f, height * 0.007f)))
                    {
                        highlight.StartCap = LineCap.Round;
                        highlight.EndCap = LineCap.Round;
                        g.DrawLine(highlight,
                            new PointF(pad + 4.0f * s, pad + 5f * s),
                            new PointF(pad + 4.0f * s, pad + 21f * s));
                    }
                }
            }

            hotX = pad + (int)Math.Round(2.6f * s);
            hotY = pad + (int)Math.Round(1.6f * s);
            return bmp;
        }

        /// <summary>
        /// The text I-beam in the same colour family as the arrow. Chrome and
        /// other editors show this cursor over text fields, so it must follow
        /// red / blue / green too; otherwise the arrow is red but the visible
        /// cursor stays blue.
        /// </summary>
        public static Bitmap CreateSignatureIBeam(int height, Color fill,
            out int hotX, out int hotY)
        {
            int size = Math.Max(20, height);
            // Keep the bitmap square and the hotspot in the middle, like the
            // standard I-beam cursor (32x32, hotspot 16,16).
            int pad = Math.Max(2, size / 16);
            int bmpSize = size + pad * 2;

            Bitmap bmp = new Bitmap(bmpSize, bmpSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float cx = bmpSize / 2f;
                float barW = size * 0.44f;
                float barH = Math.Max(3f, size * 0.11f);
                float stemW = Math.Max(3f, size * 0.12f);
                float top = pad + size * 0.10f;
                float bottom = pad + size * 0.90f;

                using (GraphicsPath path = new GraphicsPath())
                {
                    // top serif
                    AddRoundedRect(path, cx - barW / 2f, top, barW, barH, barH / 2f);
                    // bottom serif
                    AddRoundedRect(path, cx - barW / 2f, bottom - barH, barW, barH, barH / 2f);
                    // stem
                    AddRoundedRect(path, cx - stemW / 2f, top + barH * 0.55f,
                        stemW, bottom - top - barH * 1.1f, stemW / 2f);

                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new PointF(0, top), new PointF(0, bottom),
                        Lighten(fill, 0.16f), Darken(fill, 0.12f)))
                    {
                        g.FillPath(brush, path);
                    }

                    using (Pen highlight = new Pen(Color.FromArgb(70, 255, 255, 255),
                        Math.Max(1f, size * 0.015f)))
                    {
                        highlight.StartCap = LineCap.Round;
                        highlight.EndCap = LineCap.Round;
                        g.DrawLine(highlight,
                            new PointF(cx - stemW * 0.18f, top + barH * 1.2f),
                            new PointF(cx - stemW * 0.18f, bottom - barH * 1.2f));
                    }
                }
            }

            hotX = bmpSize / 2;
            hotY = bmpSize / 2;
            return bmp;
        }

        private static void AddRoundedRect(GraphicsPath path,
            float x, float y, float width, float height, float radius)
        {
            float d = Math.Min(radius * 2f, Math.Min(width, height));
            if (d <= 0.5f)
            {
                path.AddRectangle(new RectangleF(x, y, width, height));
                return;
            }

            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
        }

        private static Color Lighten(Color color, float amount)
        {
            return Color.FromArgb(color.A,
                (int)Math.Round(color.R + (255 - color.R) * amount),
                (int)Math.Round(color.G + (255 - color.G) * amount),
                (int)Math.Round(color.B + (255 - color.B) * amount));
        }

        private static Color Darken(Color color, float amount)
        {
            return Color.FromArgb(color.A,
                (int)Math.Round(color.R * (1f - amount)),
                (int)Math.Round(color.G * (1f - amount)),
                (int)Math.Round(color.B * (1f - amount)));
        }

        public static void SaveIconPreview(IntPtr hIcon, int width, int height, string path)
        {
            int pad = 20;
            using (Bitmap bmp = new Bitmap(width + pad * 2, height + pad * 2, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                using (SolidBrush a = new SolidBrush(Color.FromArgb(255, 30, 34, 42)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 44, 50, 60)))
                {
                    for (int y = 0; y < bmp.Height; y += 16)
                    {
                        for (int x = 0; x < bmp.Width; x += 16)
                        {
                            g.FillRectangle(((x / 16 + y / 16) % 2 == 0) ? a : b, x, y, 16, 16);
                        }
                    }
                }

                IntPtr hdc = g.GetHdc();
                try
                {
                    Native.DrawIconEx(hdc, pad, pad, hIcon, 0, 0, 0, IntPtr.Zero, 3);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }

                bmp.Save(path, ImageFormat.Png);
            }
        }

        public static IntPtr ToHandle(Bitmap bmp, int hotX, int hotY, bool asIcon)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            Native.BITMAPINFO bmi = new Native.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = w;
            bmi.bmiHeader.biHeight = -h;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;

            IntPtr bits;
            IntPtr hColor = Native.CreateDIBSection(IntPtr.Zero, ref bmi, 0, out bits, IntPtr.Zero, 0);
            if (hColor == IntPtr.Zero) return IntPtr.Zero;

            int maskStride = ((w + 15) / 16) * 2;
            byte[] maskBytes = new byte[maskStride * h];

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * w * 4, w * 4);
                }
                Marshal.Copy(pixels, 0, bits, pixels.Length);

                // Build the AND mask from the alpha channel. Without this the
                // cursor compositor may treat transparent pixels as opaque and
                // draw a dark box around the pointer.
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[(y * w + x) * 4 + 3];
                        if (alpha < 16)
                        {
                            maskBytes[y * maskStride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            IntPtr maskBits = Marshal.AllocHGlobal(maskBytes.Length);
            IntPtr hMask;
            try
            {
                Marshal.Copy(maskBytes, 0, maskBits, maskBytes.Length);
                hMask = Native.CreateBitmap(w, h, 1, 1, maskBits);
            }
            finally
            {
                Marshal.FreeHGlobal(maskBits);
            }

            Native.ICONINFO info = new Native.ICONINFO();
            info.fIcon = asIcon;
            info.xHotspot = hotX;
            info.yHotspot = hotY;
            info.hbmMask = hMask;
            info.hbmColor = hColor;

            IntPtr handle = Native.CreateIconIndirect(ref info);

            Native.DeleteObject(hColor);
            Native.DeleteObject(hMask);
            return handle;
        }
    }

    internal static class CursorScaler
    {
        private sealed class CursorFrame : IDisposable
        {
            public Bitmap Arrow;
            public int ArrowX;
            public int ArrowY;
            public IntPtr ArrowIcon;
            public Bitmap Beam;
            public int BeamX;
            public int BeamY;
            public IntPtr BeamIcon;

            public void Dispose()
            {
                if (Arrow != null) Arrow.Dispose();
                if (Beam != null) Beam.Dispose();
                if (ArrowIcon != IntPtr.Zero) Native.DestroyCursor(ArrowIcon);
                if (BeamIcon != IntPtr.Zero) Native.DestroyCursor(BeamIcon);
                Arrow = null;
                Beam = null;
                ArrowIcon = IntPtr.Zero;
                BeamIcon = IntPtr.Zero;
            }
        }

        private static readonly object Sync = new object();
        private static readonly Timer AnimTimer = new Timer();
        private static readonly Stopwatch AnimWatch = new Stopwatch();
        private static bool _timerReady;
        private static bool _active;
        private static bool _shrinking;
        private static List<CursorFrame> _frames;
        private static List<CursorFrame> _shrinkFrames;
        private static List<CursorFrame> _ownedFrames;
        private static int _frameIndex;
        private static int _frameDurationMs;
        private static bool _initialized;
        private static bool _sourceIsHighRes;
        private static Bitmap _source;
        private static int _sourceHotX;
        private static int _sourceHotY;
        private static IntPtr _savedArrow;
        private static IntPtr _savedIBeam;

        public static string DebugPreviewPath;

        public static bool IsActive
        {
            get { return _active; }
        }

        /// <summary>
        /// Captures the user's current system arrow once, before any magnification.
        /// Also resets a cursor left magnified by a previous crashed run.
        /// </summary>
        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized) return;
                _initialized = true;

                Bitmap captured = null;
                int hotX = 0, hotY = 0;

                // Use the exact 32 px arrow the user sees. The .cur file's
                // 128 px artwork is a different design (thin outline/tail).
                bool ok = CursorCapture.TryGetArrow(out captured, out hotX, out hotY);

                // A cursor much larger than a normal one is probably our own
                // magnified arrow left behind by a crashed run. Reset the
                // scheme, wait for the reload, then capture again.
                if (ok && captured != null && (captured.Width > 64 || captured.Height > 64))
                {
                    captured.Dispose();
                    captured = null;
                    Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                    System.Threading.Thread.Sleep(180);
                    ok = CursorCapture.TryGetArrow(out captured, out hotX, out hotY);
                }

                if (ok && captured != null)
                {
                    _source = captured;
                    _sourceHotX = hotX;
                    _sourceHotY = hotY;
                    _sourceIsHighRes = false;
                    Log.Write("cursor base " + captured.Width + "x" + captured.Height);

                    if (!string.IsNullOrEmpty(DebugPreviewPath))
                    {
                        try
                        {
                            captured.Save(System.IO.Path.ChangeExtension(DebugPreviewPath, ".base.png"),
                                ImageFormat.Png);
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    _source = CursorArt.CreateArrow(36, out hotX, out hotY);
                    _sourceHotX = hotX;
                    _sourceHotY = hotY;
                    _sourceIsHighRes = false;
                    Log.Write("cursor capture failed; using fallback arrow");
                }
            }
        }

        private static void SaveCurrentArrow()
        {
            IntPtr live = Native.LoadCursor(IntPtr.Zero, (IntPtr)Native.IDC_ARROW);
            if (live == IntPtr.Zero) return;
            if (IsLargeCursor(live)) return;

            IntPtr copy = Native.CopyIcon(live);
            if (copy == IntPtr.Zero) return;

            if (_savedArrow != IntPtr.Zero) Native.DestroyCursor(_savedArrow);
            _savedArrow = copy;
        }

        private static bool IsLargeCursor(IntPtr hCursor)
        {
            Native.ICONINFO info;
            if (!Native.GetIconInfo(hCursor, out info)) return false;

            try
            {
                Native.BITMAP bm;
                IntPtr hbm = info.hbmColor != IntPtr.Zero ? info.hbmColor : info.hbmMask;
                if (Native.GetObject(hbm, Marshal.SizeOf(typeof(Native.BITMAP)), out bm) == 0) return false;
                return bm.bmWidth > 96 || bm.bmHeight > 96;
            }
            finally
            {
                if (info.hbmMask != IntPtr.Zero) Native.DeleteObject(info.hbmMask);
                if (info.hbmColor != IntPtr.Zero) Native.DeleteObject(info.hbmColor);
            }
        }

        private static void RefreshLiveSource()
        {
            Bitmap live = null;
            int hotX = 0, hotY = 0;
            if (!CursorCapture.TryGetArrow(out live, out hotX, out hotY) || live == null) return;
            if (live.Width > 96 || live.Height > 96)
            {
                live.Dispose();
                return;
            }

            if (_source != null) _source.Dispose();
            _source = live;
            _sourceHotX = hotX;
            _sourceHotY = hotY;
            Log.Write("live source " + live.Width + "x" + live.Height + " @" + hotX + "," + hotY);
        }

        private static void EnsureTimer()
        {
            if (_timerReady) return;
            _timerReady = true;
            AnimTimer.Interval = 20;
            AnimTimer.Tick += OnAnimTick;
        }

        public static bool Magnify(double factor)
        {
            Initialize();
            EnsureTimer();

            lock (Sync)
            {
                if (_active)
                {
                    bool shrinking = _shrinking;
                    if (!shrinking)
                    {
                        // If the restore timer was delayed or the cursor was
                        // replaced externally, _active can be stale while the
                        // pointer is already back to normal. In that case a new
                        // trigger must reset and animate instead of being
                        // swallowed by "already active".
                        IntPtr arrow = Native.LoadCursor(IntPtr.Zero, (IntPtr)Native.IDC_ARROW);
                        IntPtr beam = Native.LoadCursor(IntPtr.Zero, (IntPtr)Native.IDC_IBEAM);
                        bool stillLarge =
                            (arrow != IntPtr.Zero && IsLargeCursor(arrow)) ||
                            (beam != IntPtr.Zero && IsLargeCursor(beam));

                        if (stillLarge) return true;

                        Log.Write("magnify stale active reset");
                    }

                    // Shrinking, or a stale active state: cancel and restart.
                    AnimTimer.Stop();
                    _active = false;
                    _shrinking = false;
                    RestoreSavedCursors();
                    DisposeFrames();
                }

                bool custom = !string.Equals(Settings.CursorStyle, "live", StringComparison.OrdinalIgnoreCase);
                if (!custom)
                {
                    // The IME cursor tool replaces the arrow at runtime. Re-read
                    // it on every trigger so the enlarged pointer matches the
                    // current colour/shape when following the live cursor.
                    RefreshLiveSource();
                    if (_source == null) return false;
                }

                // The factor is relative to a standard 32 px cursor. 3/4/5
                // therefore map to a clearly visible 96/128/160 px arrow.
                int targetHeight = (int)Math.Round(32.0 * factor);
                if (targetHeight < 96) targetHeight = 96;
                if (targetHeight > 260) targetHeight = 260;

                int startHeight = custom
                    ? Math.Max(24, Settings.ImeSize)
                    : Math.Max(24, _source.Height);
                if (startHeight >= targetHeight) startHeight = targetHeight;

                Color fillColor = Settings.FillColor;
                bool sampled = false;

                if (custom && string.Equals(Settings.ColorMode, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    Bitmap live = null;
                    int liveHotX, liveHotY;
                    if (CursorCapture.TryGetArrow(out live, out liveHotX, out liveHotY) && live != null)
                    {
                        Color liveFill;
                        Color liveOutline;
                        CursorVector.GetPalette(live, out liveFill, out liveOutline);
                        fillColor = liveFill;
                        sampled = true;
                        live.Dispose();
                    }
                }

                List<CursorFrame> grow = null;
                List<CursorFrame> shrink = null;
                try
                {
                    grow = BuildFrames(startHeight, targetHeight, 14, fillColor, custom, false);

                    // Reuse the same bitmaps/icons in reverse for the shrink.
                    // This halves frame memory and GDI+ build time.
                    shrink = new List<CursorFrame>(grow);
                    shrink.Reverse();
                }
                catch (Exception ex)
                {
                    Log.Write("frame build failed: " + ex.Message);
                    if (grow != null) DisposeList(grow);
                    return false;
                }

                if (grow.Count == 0)
                {
                    DisposeList(grow);
                    return false;
                }

                if (!string.IsNullOrEmpty(DebugPreviewPath))
                {
                    try { grow[grow.Count - 1].Arrow.Save(DebugPreviewPath, ImageFormat.Png); }
                    catch { }
                }

                SaveCurrentCursors();

                if (custom)
                {
                    Log.Write(string.Format("custom fill=#{0:X2}{1:X2}{2:X2} sampled={3}",
                        fillColor.R, fillColor.G, fillColor.B, sampled));
                }
                Log.Write(string.Format("animate {0}->{1}px custom={2}", startHeight, targetHeight, custom));

                _ownedFrames = grow;
                _shrinkFrames = shrink;
                _active = true;
                _shrinking = false;
                StartAnimation(grow, 280);
                return true;
            }
        }

        private static List<CursorFrame> BuildFrames(int fromHeight, int toHeight, int count,
            Color fill, bool custom, bool shrink)
        {
            List<CursorFrame> frames = new List<CursorFrame>(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                double p = count <= 1 ? 1.0 : (double)i / (count - 1);
                double eased = shrink
                    ? Math.Pow(p, 2.2)
                    : 1.0 - Math.Pow(1.0 - p, 3.0);
                int height = (int)Math.Round(fromHeight + (toHeight - fromHeight) * eased);
                if (i == count - 1) height = toHeight;
                if (height < 16) height = 16;

                CursorFrame frame = new CursorFrame();
                if (custom)
                {
                    frame.Arrow = CursorArt.CreateSignatureArrow(height, fill,
                        Settings.OutlineColor, out frame.ArrowX, out frame.ArrowY);
                    frame.Beam = CursorArt.CreateSignatureIBeam(height, fill,
                        out frame.BeamX, out frame.BeamY);
                }
                else
                {
                    frame.Arrow = CursorPixel.Upscale(_source, _sourceHotX, _sourceHotY,
                        height, out frame.ArrowX, out frame.ArrowY);
                    frame.Beam = CursorArt.CreateSignatureIBeam(height, fill,
                        out frame.BeamX, out frame.BeamY);
                }

                if (frame.Arrow == null)
                {
                    frame.Arrow = CursorArt.CreateSignatureArrow(height, fill,
                        Settings.OutlineColor, out frame.ArrowX, out frame.ArrowY);
                }

                // Cache the HICON once. SetSystemCursor takes ownership and
                // destroys the handle, so ApplyFrame copies this cached icon;
                // this avoids rebuilding a DIB + icon on every animation tick.
                frame.ArrowIcon = CursorArt.ToHandle(frame.Arrow, frame.ArrowX, frame.ArrowY, false);
                frame.BeamIcon = CursorArt.ToHandle(frame.Beam, frame.BeamX, frame.BeamY, false);
                frames.Add(frame);
                }
                return frames;
            }
            catch
            {
                DisposeList(frames);
                throw;
            }
        }

        private static void StartAnimation(List<CursorFrame> frames, int durationMs)
        {
            // Do NOT dispose the previous list here: the shrink list is the
            // same set of CursorFrame objects in reverse order.
            _frames = frames;
            _frameIndex = -1;
            _frameDurationMs = Math.Max(1, durationMs);
            AnimWatch.Restart();
            ApplyFrame(0);
            _frameIndex = 0;
            if (!AnimTimer.Enabled) AnimTimer.Start();
        }

        private static void OnAnimTick(object sender, EventArgs e)
        {
            if (!_active || _frames == null || _frames.Count == 0)
            {
                AnimTimer.Stop();
                return;
            }

            long elapsed = AnimWatch.ElapsedMilliseconds;
            int count = _frames.Count;
            int index = count <= 1 ? 0
                : (int)(elapsed * (count - 1) / Math.Max(1, _frameDurationMs));
            if (index >= count) index = count - 1;
            if (index != _frameIndex)
            {
                ApplyFrame(index);
                _frameIndex = index;
            }

            if (elapsed < _frameDurationMs) return;

            if (_shrinking)
            {
                FinishShrink();
            }
            else
            {
                AnimTimer.Stop();
            }
        }

        private static void ApplyFrame(int index)
        {
            if (_frames == null || index < 0 || index >= _frames.Count) return;
            CursorFrame frame = _frames[index];
            SetCursorIcon(frame.ArrowIcon, Native.OCR_NORMAL);
            SetCursorIcon(frame.BeamIcon, Native.OCR_IBEAM);
        }

        private static void SetCursorIcon(IntPtr icon, uint id)
        {
            if (icon == IntPtr.Zero) return;

            IntPtr copy = Native.CopyIcon(icon);
            if (copy == IntPtr.Zero) return;

            if (!Native.SetSystemCursor(copy, id))
            {
                Log.Write("SetSystemCursor failed: " + Marshal.GetLastWin32Error());
                Native.DestroyCursor(copy);
            }
        }

        private static void FinishShrink()
        {
            AnimTimer.Stop();
            _active = false;
            _shrinking = false;
            RestoreSavedCursors();
            DisposeFrames();
            Log.Write("cursor restored");
        }

        private static void SaveCurrentCursors()
        {
            SaveCursor((IntPtr)Native.IDC_ARROW, ref _savedArrow);
            SaveCursor((IntPtr)Native.IDC_IBEAM, ref _savedIBeam);
        }

        private static void SaveCursor(IntPtr id, ref IntPtr saved)
        {
            IntPtr live = Native.LoadCursor(IntPtr.Zero, id);
            if (live == IntPtr.Zero || IsLargeCursor(live)) return;

            IntPtr copy = Native.CopyIcon(live);
            if (copy == IntPtr.Zero) return;

            if (saved != IntPtr.Zero) Native.DestroyCursor(saved);
            saved = copy;
        }

        private static void RestoreSavedCursors()
        {
            bool restored = false;

            if (_savedArrow != IntPtr.Zero)
            {
                IntPtr copy = Native.CopyIcon(_savedArrow);
                if (copy != IntPtr.Zero)
                {
                    if (Native.SetSystemCursor(copy, Native.OCR_NORMAL)) restored = true;
                    else Native.DestroyCursor(copy);
                }
            }

            if (_savedIBeam != IntPtr.Zero)
            {
                IntPtr copy = Native.CopyIcon(_savedIBeam);
                if (copy != IntPtr.Zero)
                {
                    if (Native.SetSystemCursor(copy, Native.OCR_IBEAM)) restored = true;
                    else Native.DestroyCursor(copy);
                }
            }

            if (!restored)
            {
                Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            }
        }

        private static void DisposeFrames()
        {
            List<CursorFrame> owned = _ownedFrames;
            _ownedFrames = null;
            _frames = null;
            _shrinkFrames = null;
            if (owned != null) DisposeList(owned);
        }

        private static void DisposeList(List<CursorFrame> frames)
        {
            if (frames == null) return;
            foreach (CursorFrame frame in frames)
            {
                if (frame != null) frame.Dispose();
            }
        }

        /// <summary>
        /// Nearest-neighbour upscale followed by a 2x downsample keeps the edges
        /// sharp instead of the soft blur produced by bicubic scaling.
        /// </summary>
        private static Bitmap ScaleBitmap(Bitmap source, int width, int height)
        {
            int midWidth = width * 2;
            int midHeight = height * 2;

            using (Bitmap mid = new Bitmap(midWidth, midHeight, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(mid))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.CompositingMode = CompositingMode.SourceOver;
                    g.DrawImage(source, 0, 0, midWidth, midHeight);
                }

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.CompositingMode = CompositingMode.SourceOver;
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(mid, new Rectangle(0, 0, width, height),
                        0, 0, midWidth, midHeight, GraphicsUnit.Pixel, attributes);
                }
                return bmp;
            }
        }

        private static Bitmap ScaleSmooth(Bitmap source, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (ImageAttributes attributes = new ImageAttributes())
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingMode = CompositingMode.SourceOver;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source, new Rectangle(0, 0, width, height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }
            return bmp;
        }

        public static void Restore()
        {
            lock (Sync)
            {
                if (!_active) return;

                if (_shrinking) return;

                if (_shrinkFrames == null || _shrinkFrames.Count == 0)
                {
                    RestoreImmediate();
                    return;
                }

                _shrinking = true;
                StartAnimation(_shrinkFrames, 240);
            }
        }

        public static void RestoreImmediate()
        {
            lock (Sync)
            {
                AnimTimer.Stop();
                _active = false;
                _shrinking = false;
                RestoreSavedCursors();
                DisposeFrames();
            }
        }

        public static void ReleaseSaved()
        {
            lock (Sync)
            {
                if (_savedArrow != IntPtr.Zero)
                {
                    Native.DestroyCursor(_savedArrow);
                    _savedArrow = IntPtr.Zero;
                }
                if (_savedIBeam != IntPtr.Zero)
                {
                    Native.DestroyCursor(_savedIBeam);
                    _savedIBeam = IntPtr.Zero;
                }
            }
        }
    }

    internal static class Log
    {
        private static readonly string FilePath =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FindMyCursor.log");
        private static readonly object Sync = new object();
        private const long MaxBytes = 1024 * 1024;

        public static string Path
        {
            get { return FilePath; }
        }

        public static void Write(string message)
        {
            lock (Sync)
            {
                try
                {
                    System.IO.FileInfo info = new System.IO.FileInfo(FilePath);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        System.IO.File.Delete(FilePath);
                    }

                    System.IO.File.AppendAllText(FilePath,
                        DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
                catch
                {
                }
            }
        }
    }
}
