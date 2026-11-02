using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FindMyCursor
{
    /// <summary>
    /// Enlarges the live 32 px cursor with the Scale2x pixel-art algorithm.
    /// Unlike tracing or redrawing, every output colour comes from the original
    /// pixels and the silhouette is preserved exactly; the algorithm only
    /// smooths the diagonal stair-steps.
    /// </summary>
    internal static class CursorPixel
    {
        public static Bitmap Upscale(Bitmap source, int sourceHotX, int sourceHotY,
            int targetHeight, out int hotX, out int hotY)
        {
            hotX = 0;
            hotY = 0;
            if (source == null || source.Width < 2 || source.Height < 2) return null;

            double scale = (double)targetHeight / source.Height;
            int targetWidth = Math.Max(8, (int)Math.Round(source.Width * scale));
            if (targetWidth > 512) targetWidth = 512;

            int passes = 0;
            int height = source.Height;
            while (height < targetHeight && passes < 4)
            {
                height *= 2;
                passes++;
            }

            Bitmap current = (Bitmap)source.Clone();
            try
            {
                for (int i = 0; i < passes; i++)
                {
                    Bitmap next = Scale2x(current);
                    current.Dispose();
                    current = next;
                }

                Bitmap result;
                if (current.Width == targetWidth && current.Height == targetHeight)
                {
                    result = (Bitmap)current.Clone();
                }
                else
                {
                    result = ScaleSmooth(current, targetWidth, targetHeight);
                }

                hotX = (int)Math.Round(sourceHotX * scale);
                hotY = (int)Math.Round(sourceHotY * scale);
                return result;
            }
            finally
            {
                current.Dispose();
            }
        }

        private static Bitmap Scale2x(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            int[] pixels = ReadPixels(source);
            int[] output = new int[width * 2 * height * 2];
            int outWidth = width * 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int p = At(pixels, width, height, x, y);
                    int a = At(pixels, width, height, x, y - 1);
                    int b = At(pixels, width, height, x + 1, y);
                    int c = At(pixels, width, height, x - 1, y);
                    int d = At(pixels, width, height, x, y + 1);

                    int e0 = (c == a && c != d && a != b) ? a : p;
                    int e1 = (a == b && a != c && b != d) ? b : p;
                    int e2 = (d == c && d != b && c != a) ? c : p;
                    int e3 = (b == d && b != a && d != c) ? d : p;

                    int offset = (y * 2) * outWidth + x * 2;
                    output[offset] = e0;
                    output[offset + 1] = e1;
                    output[offset + outWidth] = e2;
                    output[offset + outWidth + 1] = e3;
                }
            }

            return FromPixels(output, outWidth, height * 2);
        }

        private static int At(int[] pixels, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return 0;
            return pixels[y * width + x];
        }

        private static int[] ReadPixels(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int[] pixels = new int[width * height];

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * width, width);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            // Treat every fully transparent pixel as the same value so the
            // comparisons do not get confused by leftover RGB bytes.
            for (int i = 0; i < pixels.Length; i++)
            {
                if ((pixels[i] & unchecked((int)0xFF000000)) == 0) pixels[i] = 0;
            }

            return pixels;
        }

        private static Bitmap FromPixels(int[] pixels, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(pixels, y * width, IntPtr.Add(data.Scan0, y * data.Stride), width);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }

        private static Bitmap ScaleSmooth(Bitmap source, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (ImageAttributes attributes = new ImageAttributes())
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                g.DrawImage(source, new Rectangle(0, 0, width, height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }
            return bmp;
        }
    }
}
