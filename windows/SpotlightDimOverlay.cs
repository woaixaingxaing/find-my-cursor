using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace FindMyCursor
{
    /// <summary>
    /// Static full-desktop dim layer. It has no window region and no hole:
    /// cutting a hole in a virtual-desktop window on every mouse move is what
    /// made the effect stutter. The "hole" is drawn by SpotlightLens on top,
    /// which re-draws the captured screen content around the pointer.
    /// </summary>
    internal sealed class SpotlightDimOverlay : Form
    {
        private readonly Timer _timer;
        private readonly Stopwatch _watch;
        private readonly double _dimOpacity = 0.46;
        private double _lastOpacity = -1.0;
        private bool _finished;

        public SpotlightDimOverlay(Rectangle monitor)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = monitor;
            TopMost = true;
            BackColor = Color.Black;
            Opacity = 0;

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
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_finished || IsDisposed) return;

            double t = _watch.Elapsed.TotalSeconds;
            if (t > 1.44)
            {
                Finish();
                return;
            }

            double fadeIn = SmoothStep(0.0, 0.45, t);
            double fadeOut = 1.0 - SmoothStep(1.10, 1.44, t);
            double alpha = Math.Min(fadeIn, fadeOut);
            double opacity = _dimOpacity * Math.Max(0.0, Math.Min(1.0, alpha));
            if (Math.Abs(opacity - _lastOpacity) >= 0.004)
            {
                Opacity = opacity;
                _lastOpacity = opacity;
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
            base.OnFormClosed(e);
        }
    }
}
