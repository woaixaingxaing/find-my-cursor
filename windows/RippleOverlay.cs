using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FindMyCursor
{
    /// <summary>
    /// Small lens that follows the pointer. The full desktop is dimmed by a
    /// separate static layer; this window redraws the pre-dim screen content
    /// inside a soft circle, so the pointer area looks unchanged and the edge
    /// is anti-aliased (no SetWindowRgn jaggies). Only a small bitmap is
    /// updated per tick, which keeps movement smooth.
    /// </summary>
    internal sealed class RippleOverlay : Form
    {
        private readonly Bitmap _captured;
        private readonly Rectangle _virtualScreen;
        private readonly Bitmap _lens;
        private readonly Graphics _lensGraphics;
        private readonly byte[] _lensPixels;
        private readonly byte[] _lensMask;
        private readonly int _lensStride;
        private readonly Pen _ringPen;
        private readonly Timer _timer;
        private readonly Stopwatch _watch;
        private readonly float _dpi;
        private readonly int _holeR;
        private readonly int _feather;
        private readonly int _size;
        private Point _lastCursor;
        private double _lastAlpha = -1.0;
        private byte _constantAlpha;
        private bool _finished;

        public RippleOverlay(Bitmap captured, Rectangle virtualScreen, Point cursor)
        {
            _captured = captured;
            _virtualScreen = virtualScreen;
            _lastCursor = cursor;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                _dpi = Math.Max(1f, g.DpiX / 96f);
            }
            _holeR = (int)Math.Round(100 * _dpi);
            _feather = Math.Max(2, (int)Math.Round(5 * _dpi));
            _size = (_holeR + _feather + 10) * 2;

            // Reuse one bitmap and one pixel buffer for every frame instead of
            // allocating ~1.2 MB of LOH objects at 30 fps.
            _lens = new Bitmap(_size, _size, PixelFormat.Format32bppArgb);
            _lensGraphics = Graphics.FromImage(_lens);
            BitmapData data = _lens.LockBits(
                new Rectangle(0, 0, _size, _size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            _lensStride = data.Stride;
            _lensPixels = new byte[_lensStride * _size];
            _lens.UnlockBits(data);

            // Precompute the circular alpha mask once. The radius and feather
            // are fixed for the lifetime of this overlay, so per-frame distance
            // and sqrt calculations are unnecessary.
            _lensMask = new byte[_size * _size];
            float maskCx = _size / 2f;
            float maskCy = _size / 2f;
            float innerR = _holeR - _feather;
            float featherPx = Math.Max(1f, _feather);
            for (int y = 0; y < _size; y++)
            {
                float dy = y + 0.5f - maskCy;
                int maskRow = y * _size;
                for (int x = 0; x < _size; x++)
                {
                    float dx = x + 0.5f - maskCx;
                    float distSq = dx * dx + dy * dy;
                    byte a;
                    if (distSq >= (float)(_holeR * _holeR))
                    {
                        a = 0;
                    }
                    else if (distSq <= innerR * innerR)
                    {
                        a = 255;
                    }
                    else
                    {
                        float dist = (float)Math.Sqrt(distSq);
                        float u = (dist - innerR) / featherPx;
                        if (u > 1f) u = 1f;
                        float eased = 1f - u * u * (3f - 2f * u);
                        a = (byte)Math.Max(0, Math.Min(255, Math.Round(255 * eased)));
                    }
                    _lensMask[maskRow + x] = a;
                }
            }

            _ringPen = new Pen(Color.FromArgb(165, 255, 255, 255),
                Math.Max(1.4f, 1.6f * _dpi));

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(cursor.X - _size / 2, cursor.Y - _size / 2, _size, _size);
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = true;

            _watch = Stopwatch.StartNew();
            _timer = new Timer();
            _timer.Interval = 30;
            _timer.Tick += OnTick;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
            Render(0);
        }

        private void OnTick(object sender, EventArgs e)
        {
            double t = _watch.Elapsed.TotalSeconds;
            if (t > 1.44)
            {
                Finish();
                return;
            }

            Point cursor = Cursor.Position;
            bool moved = cursor != _lastCursor;
            if (moved)
            {
                _lastCursor = cursor;
                Left = cursor.X - _size / 2;
                Top = cursor.Y - _size / 2;
            }

            double fadeIn = SmoothStep(0.0, 0.45, t);
            double fadeOut = 1.0 - SmoothStep(1.10, 1.44, t);
            double alpha = Math.Min(fadeIn, fadeOut);

            if (moved || Math.Abs(alpha - _lastAlpha) >= 0.01)
            {
                Render(t);
            }
        }

        private void Render(double t)
        {
            if (_finished || IsDisposed) return;

            double fadeIn = SmoothStep(0.0, 0.45, t);
            double fadeOut = 1.0 - SmoothStep(1.10, 1.44, t);
            double alpha = Math.Min(fadeIn, fadeOut);
            _lastAlpha = alpha;
            _constantAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(255 * alpha)));

            Graphics g = _lensGraphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Draw the original (pre-dim) screen around the pointer.
            int sx = _lastCursor.X - _virtualScreen.X - _size / 2;
            int sy = _lastCursor.Y - _virtualScreen.Y - _size / 2;
            int dx = 0;
            int dy = 0;
            int w = _size;
            int h = _size;

            if (sx < 0) { dx = -sx; w += sx; sx = 0; }
            if (sy < 0) { dy = -sy; h += sy; sy = 0; }
            if (sx + w > _captured.Width) w = _captured.Width - sx;
            if (sy + h > _captured.Height) h = _captured.Height - sy;

            if (w > 0 && h > 0)
            {
                g.DrawImage(_captured,
                    new Rectangle(dx, dy, w, h),
                    new Rectangle(sx, sy, w, h),
                    GraphicsUnit.Pixel);
            }

            ApplyCircularMask(_lens, _lensPixels);

            g.DrawEllipse(_ringPen, _size / 2f - _holeR, _size / 2f - _holeR,
                _holeR * 2, _holeR * 2);

            Push(_lens);
        }

        private void ApplyCircularMask(Bitmap bmp, byte[] pixels)
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                for (int y = 0; y < bmp.Height; y++)
                {
                    int row = y * stride;
                    int maskRow = y * bmp.Width;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        byte a = _lensMask[maskRow + x];
                        if (a == 255) continue;

                        int index = row + x * 4 + 3;
                        if (a == 0)
                        {
                            pixels[index] = 0;
                        }
                        else
                        {
                            pixels[index] = (byte)(pixels[index] * a / 255);
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private void Push(Bitmap bmp)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = Native.SelectObject(memDc, hBitmap);

                Native.POINT dest = new Native.POINT();
                dest.X = Left;
                dest.Y = Top;
                Native.SIZE size = new Native.SIZE(bmp.Width, bmp.Height);
                Native.POINT src = new Native.POINT();
                src.X = 0;
                src.Y = 0;

                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
                blend.BlendOp = 0;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = _constantAlpha;
                blend.AlphaFormat = 1;

                Native.UpdateLayeredWindow(Handle, screenDc, ref dest, ref size, memDc, ref src, 0, ref blend, 2);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) Native.SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) Native.DeleteObject(hBitmap);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Math.Max(0.0, Math.Min(1.0, (x - edge0) / (edge1 - edge0)));
            return t * t * (3.0 - 2.0 * t);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            _timer.Stop();
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            if (_lensGraphics != null) _lensGraphics.Dispose();
            if (_lens != null) _lens.Dispose();
            if (_ringPen != null) _ringPen.Dispose();
            if (_captured != null) _captured.Dispose();
            base.OnFormClosed(e);
        }
    }
}
