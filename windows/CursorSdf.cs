using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FindMyCursor
{
    /// <summary>
    /// Renders the exact silhouette of the captured cursor at a larger size.
    /// The alpha mask is converted to a signed distance field, which is sampled
    /// with bilinear interpolation and re-thresholded per target pixel. This
    /// keeps the original outline/shape while producing smooth, crisp edges
    /// instead of a blurry bitmap upscale or a lossy contour trace.
    /// </summary>
    internal static class CursorSdf
    {
        public static bool TryRender(Bitmap source, Color fill, Color outline,
            int targetWidth, int targetHeight, int outlinePixels,
            int sourceHotX, int sourceHotY,
            out Bitmap result, out int hotX, out int hotY)
        {
            result = null;
            hotX = 0;
            hotY = 0;

            int width = source.Width;
            int height = source.Height;
            if (width < 3 || height < 3 || targetWidth < 8 || targetHeight < 8) return false;

            bool[,] inside = new bool[width, height];
            int insideCount = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (source.GetPixel(x, y).A >= 128)
                    {
                        inside[x, y] = true;
                        insideCount++;
                    }
                }
            }

            if (insideCount == 0 || insideCount == width * height) return false;

            float[,] sdf = BuildSdf(inside, width, height);
            if (sdf == null) return false;

            float scaleX = (float)targetWidth / width;
            float scaleY = (float)targetHeight / height;
            float scale = Math.Min(scaleX, scaleY);
            float outlineSource = Math.Max(0.15f, outlinePixels / scale);

            Bitmap bmp = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, targetWidth, targetHeight),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                byte[] pixels = new byte[targetWidth * targetHeight * 4];

                for (int y = 0; y < targetHeight; y++)
                {
                    float sy = (y + 0.5f) * height / targetHeight - 0.5f;
                    for (int x = 0; x < targetWidth; x++)
                    {
                        float sx = (x + 0.5f) * width / targetWidth - 0.5f;
                        float distance = Sample(sdf, sx, sy, width, height);

                        float coverage = Clamp01(0.5f + distance * scale);
                        if (coverage <= 0.001f) continue;

                        float fillMix = Clamp01(0.5f + (distance - outlineSource) * scale);

                        byte r = (byte)Math.Round(outline.R + (fill.R - outline.R) * fillMix);
                        byte g = (byte)Math.Round(outline.G + (fill.G - outline.G) * fillMix);
                        byte b = (byte)Math.Round(outline.B + (fill.B - outline.B) * fillMix);
                        byte a = (byte)Math.Round(coverage * 255);

                        int index = (y * targetWidth + x) * 4;
                        pixels[index] = b;
                        pixels[index + 1] = g;
                        pixels[index + 2] = r;
                        pixels[index + 3] = a;
                    }
                }

                for (int y = 0; y < targetHeight; y++)
                {
                    Marshal.Copy(pixels, y * targetWidth * 4,
                        IntPtr.Add(data.Scan0, y * data.Stride), targetWidth * 4);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            result = bmp;
            hotX = (int)Math.Round((sourceHotX + 0.5) * targetWidth / (double)width - 0.5);
            hotY = (int)Math.Round((sourceHotY + 0.5) * targetHeight / (double)height - 0.5);
            if (hotX < 0) hotX = 0;
            if (hotY < 0) hotY = 0;
            if (hotX > targetWidth) hotX = targetWidth;
            if (hotY > targetHeight) hotY = targetHeight;
            return true;
        }

        private static float[,] BuildSdf(bool[,] inside, int width, int height)
        {
            float[,] sdf = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool sourceInside = inside[x, y];
                    float best = float.MaxValue;

                    for (int j = 0; j < height; j++)
                    {
                        for (int i = 0; i < width; i++)
                        {
                            if (inside[i, j] == sourceInside) continue;
                            float dx = x - i;
                            float dy = y - j;
                            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (distance < best) best = distance;
                        }
                    }

                    if (best == float.MaxValue) return null;

                    float magnitude = Math.Max(0f, best - 0.5f);
                    sdf[x, y] = sourceInside ? magnitude : -magnitude;
                }
            }

            return sdf;
        }

        private static float Sample(float[,] sdf, float x, float y, int width, int height)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            float fx = x - x0;
            float fy = y - y0;

            float v00 = Get(sdf, x0, y0, width, height);
            float v10 = Get(sdf, x1, y0, width, height);
            float v01 = Get(sdf, x0, y1, width, height);
            float v11 = Get(sdf, x1, y1, width, height);

            float top = v00 + (v10 - v00) * fx;
            float bottom = v01 + (v11 - v01) * fx;
            return top + (bottom - top) * fy;
        }

        private static float Get(float[,] sdf, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return -2f;
            return sdf[x, y];
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
