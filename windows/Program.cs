using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FindMyCursor
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--restore", StringComparison.OrdinalIgnoreCase))
            {
                CursorScaler.RestoreImmediate();
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--exit", StringComparison.OrdinalIgnoreCase))
            {
                // No tray icon: allow the hidden instance to be stopped from
                // a shortcut or command line without Task Manager.
                try
                {
                    using (System.Threading.EventWaitHandle exit =
                        System.Threading.EventWaitHandle.OpenExisting("Local\\FindMyCursor.Exit"))
                    {
                        exit.Set();
                    }
                }
                catch
                {
                }
                return;
            }

            bool demo = args.Length > 0 && string.Equals(args[0], "--demo", StringComparison.OrdinalIgnoreCase);
            bool silent = args.Length > 0 && string.Equals(args[0], "--silent", StringComparison.OrdinalIgnoreCase);

            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Local\\FindMyCursor.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already resident: ask it to play the
                    // animation immediately, then leave without any dialog.
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            using (System.Threading.EventWaitHandle trigger =
                                System.Threading.EventWaitHandle.OpenExisting("Local\\FindMyCursor.Trigger"))
                            {
                                trigger.Set();
                            }
                            return;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(80);
                        }
                    }
                    return;
                }

                EnableDpiAwareness();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppDomain.CurrentDomain.UnhandledException += delegate
                {
                    CursorScaler.RestoreImmediate();
                    Log.Write("unhandled exception");
                };
                Application.ThreadException += delegate
                {
                    CursorScaler.RestoreImmediate();
                    Log.Write("thread exception");
                };

                try
                {
                    using (TrayApp context = new TrayApp(demo, silent))
                    {
                        Application.Run(context);
                    }
                }
                finally
                {
                    CursorScaler.Restore();
                }
            }
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                Native.SetProcessDpiAwareness(2); // per-monitor v2
                return;
            }
            catch
            {
            }

            try
            {
                Native.SetProcessDPIAware();
            }
            catch
            {
            }
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly Icon _trayIcon;
        private readonly IntPtr _trayIconHandle;
        private readonly bool _ownsTrayIcon;
        private readonly MouseHook _hook;
        private readonly KeyboardHook _keyboardHook;
        private readonly ShakeDetector _detector = new ShakeDetector();
        private readonly Timer _restoreTimer = new Timer();
        private readonly Timer _spotlightTimer = new Timer();
        private readonly Timer _startupTimer = new Timer();
        private readonly HotkeyWindow _hotkey;
        private readonly ImeIndicator _ime;
        private readonly Control _dispatcher = new Control();
        private readonly System.Threading.EventWaitHandle _triggerEvent;
        private readonly System.Threading.EventWaitHandle _exitEvent;
        private readonly ToolStripMenuItem _enableItem;

        private RippleOverlay _overlay;
        private SpotlightDimOverlay _dimOverlay;
        private Point _spotlightPoint;
        private int _spotlightGeneration;
        private volatile bool _captureInProgress;
        private double _scale = 4.0;
        private long _lastTrigger = -100000;
        private bool _enabled = true;
        private volatile bool _disposed;

        public TrayApp(bool demo, bool silent)
        {
            IntPtr unused = _dispatcher.Handle;

            bool triggerEventCreated;
            _triggerEvent = new System.Threading.EventWaitHandle(false,
                System.Threading.EventResetMode.AutoReset, "Local\\FindMyCursor.Trigger",
                out triggerEventCreated);
            System.Threading.Thread triggerThread = new System.Threading.Thread(TriggerLoop);
            triggerThread.IsBackground = true;
            triggerThread.Start();

            bool exitEventCreated;
            _exitEvent = new System.Threading.EventWaitHandle(false,
                System.Threading.EventResetMode.AutoReset, "Local\\FindMyCursor.Exit",
                out exitEventCreated);
            System.Threading.Thread exitThread = new System.Threading.Thread(ExitLoop);
            exitThread.IsBackground = true;
            exitThread.Start();

            Settings.Load();
            Settings.EnsureFile();
            _scale = Settings.Scale;
            _enabled = Settings.Enabled;

            if (demo)
            {
                CursorScaler.DebugPreviewPath =
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FindMyCursor-preview.png");
            }

            CursorScaler.Initialize();
            _ime = new ImeIndicator();
            _ime.Enabled = Settings.ImeIndicator;

            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem triggerItem = new ToolStripMenuItem("立即放大指针    Ctrl+Alt+F");
            triggerItem.Click += delegate { TriggerAtCursor(); };
            menu.Items.Add(triggerItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem scaleMenu = new ToolStripMenuItem("指针大小");
            double[] scales = new double[] { 3.0, 4.0, 5.0 };
            string[] scaleLabels = new string[] { "标准 · 96 px", "大 · 128 px", "超大 · 160 px" };
            for (int i = 0; i < scales.Length; i++)
            {
                double value = scales[i];
                ToolStripMenuItem item = new ToolStripMenuItem(scaleLabels[i]);
                item.Tag = value;
                item.Checked = Math.Abs(value - _scale) < 0.01;
                item.Click += delegate(object sender, EventArgs e)
                {
                    ToolStripMenuItem clicked = (ToolStripMenuItem)sender;
                    _scale = (double)clicked.Tag;
                    foreach (ToolStripItem entry in scaleMenu.DropDownItems)
                    {
                        ToolStripMenuItem other = entry as ToolStripMenuItem;
                        if (other != null && other.Tag is double) other.Checked = other == clicked;
                    }
                    Settings.Scale = _scale;
                    Settings.Save();
                    Log.Write("scale = " + _scale.ToString("0.0"));
                };
                scaleMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(scaleMenu);

            ToolStripMenuItem colorMenu = new ToolStripMenuItem("指针颜色");
            ToolStripMenuItem autoColorItem = new ToolStripMenuItem("跟随当前指针");
            ToolStripMenuItem fixedColorItem = new ToolStripMenuItem("使用配置文件颜色");
            autoColorItem.Checked = !string.Equals(Settings.ColorMode, "fixed", StringComparison.OrdinalIgnoreCase);
            fixedColorItem.Checked = !autoColorItem.Checked;
            autoColorItem.Click += delegate
            {
                Settings.ColorMode = "auto";
                autoColorItem.Checked = true;
                fixedColorItem.Checked = false;
                Settings.Save();
            };
            fixedColorItem.Click += delegate
            {
                Settings.ColorMode = "fixed";
                autoColorItem.Checked = false;
                fixedColorItem.Checked = true;
                Settings.Save();
            };
            colorMenu.DropDownItems.Add(autoColorItem);
            colorMenu.DropDownItems.Add(fixedColorItem);
            colorMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem openConfigItem = new ToolStripMenuItem("打开配置文件");
            openConfigItem.Click += delegate
            {
                try { System.Diagnostics.Process.Start("notepad.exe", Settings.ConfigPath); }
                catch { }
            };
            colorMenu.DropDownItems.Add(openConfigItem);
            menu.Items.Add(colorMenu);

            ToolStripMenuItem styleMenu = new ToolStripMenuItem("指针风格");
            ToolStripMenuItem customStyleItem = new ToolStripMenuItem("自定义（推荐）");
            ToolStripMenuItem liveStyleItem = new ToolStripMenuItem("跟随当前指针");
            customStyleItem.Checked = !string.Equals(Settings.CursorStyle, "live", StringComparison.OrdinalIgnoreCase);
            liveStyleItem.Checked = !customStyleItem.Checked;
            customStyleItem.Click += delegate
            {
                Settings.CursorStyle = "custom";
                customStyleItem.Checked = true;
                liveStyleItem.Checked = false;
                Settings.Save();
            };
            liveStyleItem.Click += delegate
            {
                Settings.CursorStyle = "live";
                customStyleItem.Checked = false;
                liveStyleItem.Checked = true;
                Settings.Save();
            };
            styleMenu.DropDownItems.Add(customStyleItem);
            styleMenu.DropDownItems.Add(liveStyleItem);
            menu.Items.Add(styleMenu);

            ToolStripMenuItem imeItem = new ToolStripMenuItem("输入法指示");
            imeItem.Checked = Settings.ImeIndicator;
            imeItem.CheckOnClick = true;
            imeItem.CheckedChanged += delegate
            {
                Settings.ImeIndicator = imeItem.Checked;
                _ime.Enabled = imeItem.Checked;
                Settings.Save();
            };
            menu.Items.Add(imeItem);

            ToolStripMenuItem imeColorMenu = new ToolStripMenuItem("输入法颜色");
            ToolStripMenuItem chineseItem = new ToolStripMenuItem("中文 · 红");
            chineseItem.Click += delegate
            {
                ImeIndicator.SetChinese();
                _ime.ApplyStateNow();
            };
            ToolStripMenuItem englishItem = new ToolStripMenuItem("英文 · 蓝");
            englishItem.Click += delegate
            {
                ImeIndicator.SetEnglish();
                _ime.ApplyStateNow();
            };
            imeColorMenu.DropDownItems.Add(chineseItem);
            imeColorMenu.DropDownItems.Add(englishItem);
            menu.Items.Add(imeColorMenu);

            _enableItem = new ToolStripMenuItem("启用摇动检测");
            _enableItem.Checked = _enabled;
            _enableItem.CheckOnClick = true;
            _enableItem.CheckedChanged += delegate
            {
                _enabled = _enableItem.Checked;
                Settings.Enabled = _enabled;
                Settings.Save();
                if (!_enabled)
                {
                    _restoreTimer.Stop();
                    CursorScaler.Restore();
                }
                Log.Write("enabled = " + _enabled);
            };
            menu.Items.Add(_enableItem);

            ToolStripMenuItem rippleItem = new ToolStripMenuItem("显示聚光灯");
            rippleItem.Checked = Settings.Ripple;
            rippleItem.CheckOnClick = true;
            rippleItem.CheckedChanged += delegate
            {
                Settings.Ripple = rippleItem.Checked;
                Settings.Save();
            };
            menu.Items.Add(rippleItem);

            ToolStripMenuItem enlargeItem = new ToolStripMenuItem("放大光标");
            enlargeItem.Checked = Settings.Enlarge;
            enlargeItem.CheckOnClick = true;
            enlargeItem.CheckedChanged += delegate
            {
                Settings.Enlarge = enlargeItem.Checked;
                Settings.Save();
                Log.Write("enlarge = " + Settings.Enlarge);
            };
            menu.Items.Add(enlargeItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem quitItem = new ToolStripMenuItem("退出");
            quitItem.Click += delegate { Quit(); };
            menu.Items.Add(quitItem);

            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            if (appIcon != null)
            {
                _trayIcon = appIcon;
                _trayIconHandle = IntPtr.Zero;
                _ownsTrayIcon = true;
            }
            else
            {
                int hotX, hotY;
                using (Bitmap iconBmp = CursorArt.CreateArrow(30, out hotX, out hotY))
                {
                    _trayIconHandle = CursorArt.ToHandle(iconBmp, 0, 0, true);
                }

                if (_trayIconHandle != IntPtr.Zero)
                {
                    _trayIcon = Icon.FromHandle(_trayIconHandle);
                    _ownsTrayIcon = true;
                }
                else
                {
                    _trayIcon = SystemIcons.Application;
                    _ownsTrayIcon = false;
                }
            }

            if (Settings.ShowTray)
            {
                _tray = new NotifyIcon();
                _tray.Icon = _trayIcon;
                _tray.Text = "找找鼠标 · 摇一摇定位指针";
                _tray.Visible = true;
                _tray.ContextMenuStrip = menu;
                _tray.MouseClick += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Left) TriggerAtCursor();
                };
                Log.Write("tray icon shown");
            }
            else
            {
                Log.Write("tray icon hidden");
            }

            _restoreTimer.Interval = 1500;
            _restoreTimer.Tick += delegate
            {
                _restoreTimer.Stop();
                _spotlightGeneration++; // invalidate a capture still in flight
                CursorScaler.Restore();
            };

            // Wait until the cursor has finished growing before creating the
            // full-desktop dim window. Showing it first blocks the UI thread
            // long enough to skip the grow animation.
            _spotlightTimer.Interval = 300;
            _spotlightTimer.Tick += delegate
            {
                _spotlightTimer.Stop();
                ShowSpotlight(_spotlightPoint, _spotlightGeneration);
            };

            _keyboardHook = new KeyboardHook();
            _keyboardHook.RightShiftDoublePressed += delegate
            {
                try
                {
                    _dispatcher.BeginInvoke((MethodInvoker)delegate { TriggerAtCursor(); });
                }
                catch
                {
                }
            };
            if (!_keyboardHook.Start()) Log.Write("keyboard hook failed");

            _hook = new MouseHook();
            _hook.MouseMoved += OnMouseMoved;
            if (!_hook.Start())
            {
                Log.Write("mouse hook failed");
                if (_tray != null)
                {
                    _tray.ShowBalloonTip(4000, "找找鼠标", "全局鼠标监听启动失败，可以用 Ctrl+Alt+F 手动触发。",
                        ToolTipIcon.Warning);
                }
            }
            else
            {
                Log.Write("mouse hook started");
            }

            _hotkey = new HotkeyWindow();
            _hotkey.Pressed += delegate { TriggerAtCursor(); };
            if (!_hotkey.Registered) Log.Write("hotkey register failed");

            // Play the effect as soon as the app starts, so pressing the icon
            // shows the animation directly instead of just parking in the tray.
            _startupTimer.Interval = demo ? 700 : 250;
            _startupTimer.Tick += delegate
            {
                _startupTimer.Stop();
                if (demo || (!silent && _enabled)) TriggerAtCursor();
            };
            _startupTimer.Start();
        }

        private void TriggerLoop()
        {
            while (true)
            {
                if (!_triggerEvent.WaitOne(500)) continue;
                if (_disposed) break;

                try
                {
                    _dispatcher.BeginInvoke((MethodInvoker)delegate { TriggerAtCursor(); });
                }
                catch
                {
                }
            }
        }

        private void ExitLoop()
        {
            while (true)
            {
                if (!_exitEvent.WaitOne(500)) continue;
                if (_disposed) break;

                try
                {
                    _dispatcher.BeginInvoke((MethodInvoker)delegate { Quit(); });
                }
                catch
                {
                }
                break;
            }
        }

        private void OnMouseMoved(int x, int y)
        {
            if (!_enabled) return;
            if (!_detector.Feed(x, y)) return;

            try
            {
                _dispatcher.BeginInvoke((MethodInvoker)delegate { Trigger(x, y); });
            }
            catch
            {
                Trigger(x, y);
            }
        }

        private void TriggerAtCursor()
        {
            Point p = Cursor.Position;
            Trigger(p.X, p.Y);
        }

        private void Trigger(int x, int y)
        {
            if (_disposed || !_enabled) return;

            long now = AppClock.Milliseconds;
            if (now - _lastTrigger < 900) return;
            _lastTrigger = now;

            if (_overlay != null)
            {
                try { _overlay.Close(); }
                catch { }
                _overlay = null;
            }
            if (_dimOverlay != null)
            {
                try { _dimOverlay.Close(); }
                catch { }
                _dimOverlay = null;
            }

            // Start the cursor grow first; the spotlight is created after the
            // grow finishes (see _spotlightTimer). Creating the full-desktop
            // dim window first blocks the UI thread and makes the grow skip.
            bool ok = true;
            if (Settings.Enlarge) ok = CursorScaler.Magnify(_scale);
            Log.Write(string.Format("trigger at {0},{1} scale {2:0.0} cursor={3}", x, y, _scale,
                Settings.Enlarge ? ok.ToString() : "off"));

            if (Settings.Ripple)
            {
                _spotlightGeneration++;
                _spotlightPoint = new Point(x, y);
                _spotlightTimer.Stop();
                _spotlightTimer.Interval = Settings.Enlarge ? 300 : 30;
                _spotlightTimer.Start();
            }

            _restoreTimer.Stop();
            _restoreTimer.Start();
        }

        private void ShowSpotlight(Point point, int generation)
        {
            if (_disposed || !Settings.Ripple || _captureInProgress) return;

            _captureInProgress = true;
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            Point center = Cursor.Position;

            // CopyFromScreen on the UI thread can block for seconds when a
            // full-screen/exclusive window or a busy display driver is in the
            // foreground. That used to starve the restore timer and leave
            // _active stuck. Capture on a worker and come back to the UI only
            // to create the small overlays.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap captured = null;
                long started = AppClock.Milliseconds;
                try
                {
                    captured = new Bitmap(virtualScreen.Width, virtualScreen.Height,
                        PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(captured))
                    {
                        g.CopyFromScreen(virtualScreen.Left, virtualScreen.Top, 0, 0,
                            virtualScreen.Size);
                    }
                    Log.Write("spotlight capture done " + (AppClock.Milliseconds - started) + "ms");
                }
                catch (Exception ex)
                {
                    Log.Write("spotlight capture failed: " + ex.Message);
                }

                try
                {
                    _dispatcher.BeginInvoke((MethodInvoker)delegate
                    {
                        _captureInProgress = false;

                        if (_disposed || generation != _spotlightGeneration || !Settings.Ripple)
                        {
                            if (captured != null) captured.Dispose();
                            return;
                        }
                        if (captured == null) return;

                        ShowSpotlightOverlays(captured, virtualScreen, center);
                    });
                }
                catch
                {
                    _captureInProgress = false;
                    if (captured != null) captured.Dispose();
                }
            });
        }

        private void ShowSpotlightOverlays(Bitmap captured, Rectangle virtualScreen, Point center)
        {
            Log.Write("spotlight overlays show");
            SpotlightDimOverlay dim = new SpotlightDimOverlay(virtualScreen);
            dim.FormClosed += delegate { _dimOverlay = null; };
            _dimOverlay = dim;
            dim.Show();

            RippleOverlay overlay = new RippleOverlay(captured, virtualScreen, center);
            overlay.FormClosed += delegate { _overlay = null; };
            _overlay = overlay;
            overlay.Show();
        }

        private void Quit()
        {
            _restoreTimer.Stop();
            _spotlightTimer.Stop();
            CursorScaler.RestoreImmediate();
            if (_overlay != null)
            {
                try { _overlay.Close(); }
                catch { }
                _overlay = null;
            }
            if (_dimOverlay != null)
            {
                try { _dimOverlay.Close(); }
                catch { }
                _dimOverlay = null;
            }
            if (_tray != null) _tray.Visible = false;
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                Cleanup();
            }
            base.Dispose(disposing);
        }

        private void Cleanup()
        {
            try { _triggerEvent.Set(); }
            catch { }

            _startupTimer.Stop();
            _startupTimer.Dispose();
            _restoreTimer.Stop();
            _restoreTimer.Dispose();
            _spotlightTimer.Stop();
            _spotlightTimer.Dispose();
            CursorScaler.RestoreImmediate();
            CursorScaler.ReleaseSaved();
            _ime.Dispose();

            if (_overlay != null)
            {
                try { _overlay.Close(); }
                catch { }
                _overlay = null;
            }
            if (_dimOverlay != null)
            {
                try { _dimOverlay.Close(); }
                catch { }
                _dimOverlay = null;
            }

            _hotkey.Dispose();
            _hook.Dispose();
            _keyboardHook.Dispose();

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            if (_ownsTrayIcon)
            {
                _trayIcon.Dispose();
                if (_trayIconHandle != IntPtr.Zero) Native.DestroyIcon(_trayIconHandle);
            }

            _dispatcher.Dispose();
        }
    }
}
