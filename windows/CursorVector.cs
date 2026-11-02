using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FindMyCursor
{
    /// <summary>
    /// Re-draws a captured cursor at a larger size by tracing its silhouette and
    /// filling it with the cursor's own colours. This keeps the shape and colour
    /// of the user's pointer while producing crisp edges at any size, instead of
    /// the blur (bicubic) or stair-steps (nearest) of bitmap scaling.
    /// </summary>
    internal static class CursorVector
    {
        private sealed class Edge
        {
            public int X1;
            public int Y1;
            public int X2;
            public int Y2;
            public bool Used;
        }

        /// <summary>
        /// Samples the cursor's main fill colour and its contrasting outline
        /// colour, so a crisp redrawn arrow keeps the user's colour scheme.
        /// </summary>
        public static void GetPalette(Bitmap source, out Color fill, out Color outline)
        {
            fill = Color.White;
            outline = Color.Black;

            bool[,] inside = BuildMask(source);
            if (inside == null) return;

            Color sampledFill;
            Color sampledOutline;
            if (PickColors(source, inside, source.Width, source.Height, out sampledFill, out sampledOutline))
            {
                fill = sampledFill;
                outline = sampledOutline;
            }
            else
            {
                // Still use the dominant colour, with a contrasting outline.
                fill = DominantColor(source);
                outline = fill.GetBrightness() > 0.55f ? Color.FromArgb(255, 18, 20, 26)
                                                       : Color.FromArgb(255, 250, 250, 252);
            }

            if (ColorDistance(fill, outline) < 40)
            {
                outline = fill.GetBrightness() > 0.55f ? Color.FromArgb(255, 18, 20, 26)
                                                       : Color.FromArgb(255, 250, 250, 252);
            }
        }

        private static Color DominantColor(Bitmap source)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    if (c.A < 200) continue;
                    int key = (c.R << 16) | (c.G << 8) | c.B;
                    int n;
                    counts.TryGetValue(key, out n);
                    counts[key] = n + 1;
                }
            }

            int bestKey = 0;
            int bestCount = -1;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    bestKey = pair.Key;
                }
            }

            if (bestCount < 0) return Color.White;
            return Color.FromArgb(255, (bestKey >> 16) & 0xFF, (bestKey >> 8) & 0xFF, bestKey & 0xFF);
        }

        public static bool TryCreate(Bitmap source, int sourceHotX, int sourceHotY,
            int targetWidth, int targetHeight, float outlinePixels,
            Color? fixedFill, Color? fixedOutline,
            out Bitmap result, out int resultHotX, out int resultHotY)
        {
            result = null;
            resultHotX = 0;
            resultHotY = 0;

            int width = source.Width;
            int height = source.Height;
            if (width < 4 || height < 4 || targetWidth < 8 || targetHeight < 8) return false;

            bool[,] inside = BuildMask(source);
            if (inside == null) return false;

            List<List<PointF>> loops = TraceLoops(inside, width, height);
            if (loops.Count == 0) return false;

            List<PointF> contour = loops[0];
            double bestArea = Math.Abs(Area(contour));
            for (int i = 1; i < loops.Count; i++)
            {
                double area = Math.Abs(Area(loops[i]));
                if (area > bestArea)
                {
                    bestArea = area;
                    contour = loops[i];
                }
            }
            if (contour.Count < 3) return false;

            Color fill;
            Color outline;
            if (fixedFill.HasValue && fixedOutline.HasValue)
            {
                fill = fixedFill.Value;
                outline = fixedOutline.Value;
            }
            else if (!PickColors(source, inside, width, height, out fill, out outline))
            {
                return false;
            }
            if (ColorDistance(fill, outline) < 24) outline = fill;

            List<PointF> simplified = SimplifyClosed(contour, 0.75);
            if (simplified.Count < 3) return false;

            float scaleX = (float)targetWidth / width;
            float scaleY = (float)targetHeight / height;
            float stroke = outline == fill ? 0f
                : (outlinePixels > 0f ? outlinePixels : Math.Max(1.2f, Math.Min(scaleX, scaleY) * 0.6f));
            int pad = (int)Math.Ceiling(stroke / 2f) + 1;

            Bitmap bmp = new Bitmap(targetWidth + pad * 2, targetHeight + pad * 2,
                PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                PointF[] points = new PointF[simplified.Count];
                for (int i = 0; i < simplified.Count; i++)
                {
                    points[i] = new PointF(
                        pad + simplified[i].X * scaleX,
                        pad + simplified[i].Y * scaleY);
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(points);

                    using (SolidBrush brush = new SolidBrush(fill))
                    {
                        g.FillPath(brush, path);
                    }

                    if (stroke > 0f)
                    {
                        // Draw the outline inside the silhouette so the outer
                        // shape stays identical to the original cursor and the
                        // band keeps the original thickness (instead of losing
                        // half the pen width outside the shape).
                        GraphicsState state = g.Save();
                        g.SetClip(path, CombineMode.Intersect);
                        using (Pen pen = new Pen(outline, stroke * 2f))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPath(pen, path);
                        }
                        g.Restore(state);
                    }
                }
            }

            result = bmp;
            resultHotX = (int)Math.Round(pad + sourceHotX * scaleX);
            resultHotY = (int)Math.Round(pad + sourceHotY * scaleY);
            return true;
        }

        private static bool[,] BuildMask(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            bool[,] inside = new bool[width, height];
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (source.GetPixel(x, y).A >= 128)
                    {
                        inside[x, y] = true;
                        count++;
                    }
                }
            }

            if (count < 16) return null;
            return inside;
        }

        private static bool PickColors(Bitmap source, bool[,] inside, int width, int height,
            out Color fill, out Color outline)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            int opaque = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    if (c.A < 200) continue;
                    int key = (c.R << 16) | (c.G << 8) | c.B;
                    int n;
                    counts.TryGetValue(key, out n);
                    counts[key] = n + 1;
                    opaque++;
                }
            }

            if (opaque == 0) { fill = Color.White; outline = Color.Black; return false; }
            if (counts.Count > 96) { fill = Color.White; outline = Color.Black; return false; }

            int bestKey = 0;
            int bestCount = -1;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    bestKey = pair.Key;
                }
            }

            if (bestCount < opaque * 0.18) { fill = Color.White; outline = Color.Black; return false; }
            fill = Color.FromArgb(255, (bestKey >> 16) & 0xFF, (bestKey >> 8) & 0xFF, bestKey & 0xFF);

            Dictionary<int, int> borderCounts = new Dictionary<int, int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!inside[x, y]) continue;
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1
                        || !inside[x - 1, y] || !inside[x + 1, y] || !inside[x, y - 1] || !inside[x, y + 1];
                    if (!border) continue;

                    Color c = source.GetPixel(x, y);
                    if (c.A < 200) continue;
                    int key = (c.R << 16) | (c.G << 8) | c.B;
                    int n;
                    borderCounts.TryGetValue(key, out n);
                    borderCounts[key] = n + 1;
                }
            }

            Color bestOutline = fill;
            int bestOutlineCount = 0;
            foreach (KeyValuePair<int, int> pair in borderCounts)
            {
                Color c = Color.FromArgb(255, (pair.Key >> 16) & 0xFF, (pair.Key >> 8) & 0xFF, pair.Key & 0xFF);
                if (ColorDistance(c, fill) < 30) continue;
                if (pair.Value > bestOutlineCount)
                {
                    bestOutlineCount = pair.Value;
                    bestOutline = c;
                }
            }

            outline = bestOutline;
            return true;
        }

        private static List<List<PointF>> TraceLoops(bool[,] inside, int width, int height)
        {
            List<Edge> edges = new List<Edge>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!inside[x, y]) continue;

                    if (x == 0 || !inside[x - 1, y]) AddEdge(edges, x, y + 1, x, y);
                    if (y == 0 || !inside[x, y - 1]) AddEdge(edges, x, y, x + 1, y);
                    if (x == width - 1 || !inside[x + 1, y]) AddEdge(edges, x + 1, y, x + 1, y + 1);
                    if (y == height - 1 || !inside[x, y + 1]) AddEdge(edges, x + 1, y + 1, x, y + 1);
                }
            }

            Dictionary<long, List<Edge>> byStart = new Dictionary<long, List<Edge>>();
            for (int i = 0; i < edges.Count; i++)
            {
                long key = PointKey(edges[i].X1, edges[i].Y1);
                List<Edge> list;
                if (!byStart.TryGetValue(key, out list))
                {
                    list = new List<Edge>();
                    byStart[key] = list;
                }
                list.Add(edges[i]);
            }

            List<List<PointF>> loops = new List<List<PointF>>();

            for (int i = 0; i < edges.Count; i++)
            {
                Edge first = edges[i];
                if (first.Used) continue;

                List<PointF> points = new List<PointF>();
                points.Add(new PointF(first.X1, first.Y1));
                first.Used = true;

                int cx = first.X2;
                int cy = first.Y2;
                points.Add(new PointF(cx, cy));

                bool closed = false;
                for (int guard = 0; guard < 20000; guard++)
                {
                    if (cx == first.X1 && cy == first.Y1)
                    {
                        closed = true;
                        break;
                    }

                    Edge next = FindNext(byStart, cx, cy);
                    if (next == null) break;

                    next.Used = true;
                    cx = next.X2;
                    cy = next.Y2;
                    points.Add(new PointF(cx, cy));
                }

                if (!closed) continue;
                if (points.Count > 1)
                {
                    PointF lastPoint = points[points.Count - 1];
                    if (lastPoint.X == points[0].X && lastPoint.Y == points[0].Y)
                    {
                        points.RemoveAt(points.Count - 1);
                    }
                }
                if (points.Count >= 4) loops.Add(points);
            }

            return loops;
        }

        private static void AddEdge(List<Edge> edges, int x1, int y1, int x2, int y2)
        {
            Edge edge = new Edge();
            edge.X1 = x1;
            edge.Y1 = y1;
            edge.X2 = x2;
            edge.Y2 = y2;
            edges.Add(edge);
        }

        private static Edge FindNext(Dictionary<long, List<Edge>> byStart, int x, int y)
        {
            List<Edge> list;
            if (!byStart.TryGetValue(PointKey(x, y), out list)) return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Used) return list[i];
            }
            return null;
        }

        private static long PointKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private static double Area(List<PointF> points)
        {
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                PointF a = points[i];
                PointF b = points[(i + 1) % points.Count];
                area += (double)a.X * b.Y - (double)b.X * a.Y;
            }
            return area * 0.5;
        }

        private static List<PointF> SimplifyClosed(List<PointF> points, double epsilon)
        {
            List<PointF> result = new List<PointF>();
            if (points.Count < 4) return new List<PointF>(points);

            int far = 0;
            double farDistance = -1;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].X - points[0].X;
                double dy = points[i].Y - points[0].Y;
                double distance = dx * dx + dy * dy;
                if (distance > farDistance)
                {
                    farDistance = distance;
                    far = i;
                }
            }

            List<PointF> closed = new List<PointF>(points);
            closed.Add(points[0]);

            result.Add(points[0]);
            SimplifySection(closed, 0, far, epsilon, result);
            result.Add(points[far]);
            SimplifySection(closed, far, closed.Count - 1, epsilon, result);

            if (result.Count > 1)
            {
                PointF last = result[result.Count - 1];
                if (last.X == result[0].X && last.Y == result[0].Y)
                {
                    result.RemoveAt(result.Count - 1);
                }
            }

            return result;
        }

        private static void SimplifySection(List<PointF> points, int first, int last,
            double epsilon, List<PointF> output)
        {
            if (last <= first + 1) return;

            double maxDistance = 0;
            int index = -1;

            for (int i = first + 1; i < last; i++)
            {
                double distance = PerpendicularDistance(points[i], points[first], points[last]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    index = i;
                }
            }

            if (maxDistance > epsilon && index != -1)
            {
                SimplifySection(points, first, index, epsilon, output);
                output.Add(points[index]);
                SimplifySection(points, index, last, epsilon, output);
            }
        }

        private static double PerpendicularDistance(PointF point, PointF lineStart, PointF lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 0.0001) return 0;

            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared;
            if (t < 0 || t > 1) return 0;

            double px = lineStart.X + t * dx;
            double py = lineStart.Y + t * dy;
            double ex = point.X - px;
            double ey = point.Y - py;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        private static int ColorDistance(Color a, Color b)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            return Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db);
        }
    }
}
