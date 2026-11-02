using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FindMyCursor
{
    internal static class CursorCapture
    {
        /// <summary>
        /// Reads the current system arrow cursor as a bitmap, keeping its exact
        /// colors and shape. Falls back to false if the cursor cannot be read.
        /// </summary>
        public static bool TryGetArrow(out Bitmap bitmap, out int hotX, out int hotY)
        {
            bitmap = null;
            hotX = 0;
            hotY = 0;

            IntPtr shared = Native.LoadCursor(IntPtr.Zero, (IntPtr)Native.IDC_ARROW);
            if (shared == IntPtr.Zero) return false;

            IntPtr copy = Native.CopyIcon(shared);
            if (copy == IntPtr.Zero) return false;

            return TryExtract(copy, "capture", out bitmap, out hotX, out hotY);
        }

        /// <summary>
        /// Loads the largest image stored in the cursor's .cur file. Windows
        /// normally hands out only the 32 px variant, but aero_arrow.cur (and
        /// most cursor packs) also contains a native 128 px image that matches
        /// the user's pointer exactly and is far sharper when enlarged.
        /// </summary>
        public static bool TryGetHighResArrow(out Bitmap bitmap, out int hotX, out int hotY)
        {
            bitmap = null;
            hotX = 0;
            hotY = 0;

            string path = null;
            try
            {
                object value = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors", "Arrow", null);
                path = value as string;
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Cursors", "aero_arrow.cur");
            }

            path = Environment.ExpandEnvironmentVariables(path);
            if (!File.Exists(path)) return false;

            IntPtr hCursor = Native.LoadImage(IntPtr.Zero, path, 2, 128, 128, 0x10);
            if (hCursor == IntPtr.Zero) return false;

            return TryExtract(hCursor, "high-res", out bitmap, out hotX, out hotY);
        }

        private static bool TryExtract(IntPtr hCursor, string label,
            out Bitmap bitmap, out int hotX, out int hotY)
        {
            bitmap = null;
            hotX = 0;
            hotY = 0;

            Native.ICONINFO info = new Native.ICONINFO();
            bool hasInfo = Native.GetIconInfo(hCursor, out info);
            bool success = false;

            try
            {
                if (!hasInfo || info.hbmColor == IntPtr.Zero) return false;

                Native.BITMAP bm;
                if (Native.GetObject(info.hbmColor, Marshal.SizeOf(typeof(Native.BITMAP)), out bm) == 0) return false;
                int width = bm.bmWidth;
                int height = bm.bmHeight;
                if (width <= 0 || height <= 0 || width > 1024 || height > 1024) return false;

                byte[] pixels = ReadPixels(info.hbmColor, width, height);
                if (pixels == null) return false;

                bool hasAlpha = HasAlpha(pixels);
                if (!hasAlpha) ApplyMaskAlpha(info.hbmMask, pixels, width, height);

                Rectangle bounds = AlphaBounds(pixels, width, height);
                Log.Write(string.Format("{0} {1}x{2} alpha={3} bounds={4}x{5}@{6},{7}",
                    label, width, height, hasAlpha, bounds.Width, bounds.Height, bounds.X, bounds.Y));
                if (bounds.Width <= 0 || bounds.Height <= 0) return false;

                using (Bitmap full = ToBitmap(pixels, width, height))
                {
                    bitmap = full.Clone(bounds, PixelFormat.Format32bppArgb);
                }

                hotX = info.xHotspot - bounds.X;
                hotY = info.yHotspot - bounds.Y;
                if (hotX < 0) hotX = 0;
                if (hotY < 0) hotY = 0;
                if (hotX > bitmap.Width) hotX = bitmap.Width;
                if (hotY > bitmap.Height) hotY = bitmap.Height;

                success = true;
            }
            finally
            {
                if (info.hbmMask != IntPtr.Zero) Native.DeleteObject(info.hbmMask);
                if (info.hbmColor != IntPtr.Zero) Native.DeleteObject(info.hbmColor);
                Native.DestroyCursor(hCursor);
            }

            return success;
        }

        private static byte[] ReadPixels(IntPtr hBitmap, int width, int height)
        {
            Native.BITMAPINFO bmi = new Native.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;

            byte[] pixels = new byte[width * height * 4];
            IntPtr dc = Native.CreateCompatibleDC(IntPtr.Zero);
            try
            {
                int lines = Native.GetDIBits(dc, hBitmap, 0, (uint)height, pixels, ref bmi, 0);
                return lines == height ? pixels : null;
            }
            finally
            {
                Native.DeleteDC(dc);
            }
        }

        private static bool HasAlpha(byte[] pixels)
        {
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) return true;
            }
            return false;
        }

        private static void ApplyMaskAlpha(IntPtr hMask, byte[] pixels, int width, int height)
        {
            if (hMask == IntPtr.Zero)
            {
                for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
                return;
            }

            int stride = ((width + 15) / 16) * 2;
            byte[] mask = new byte[stride * height];

            Native.BITMAPINFO bmi = new Native.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 1;
            bmi.bmiHeader.biCompression = 0;

            IntPtr dc = Native.CreateCompatibleDC(IntPtr.Zero);
            int lines;
            try
            {
                lines = Native.GetDIBits(dc, hMask, 0, (uint)height, mask, ref bmi, 0);
            }
            finally
            {
                Native.DeleteDC(dc);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool transparent = false;
                    if (lines == height)
                    {
                        int row = y * stride + (x >> 3);
                        int bit = 0x80 >> (x & 7);
                        transparent = (mask[row] & bit) != 0;
                    }
                    pixels[(y * width + x) * 4 + 3] = transparent ? (byte)0 : (byte)255;
                }
            }
        }

        private static Rectangle AlphaBounds(byte[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[(y * width + x) * 4 + 3] == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return Rectangle.Empty;

            int left = Math.Max(0, minX - 1);
            int top = Math.Max(0, minY - 1);
            int right = Math.Min(width - 1, maxX + 1);
            int bottom = Math.Min(height - 1, maxY + 1);

            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        private static Bitmap ToBitmap(byte[] pixels, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(pixels, y * width * 4,
                        IntPtr.Add(data.Scan0, y * data.Stride), width * 4);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
    }
}
