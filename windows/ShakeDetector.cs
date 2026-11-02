using System;

namespace FindMyCursor
{
    internal sealed class ShakeDetector
    {
        private struct Sample
        {
            public int X;
            public int Y;
            public long T;
        }

        // Ring buffer: no List.RemoveAt(0) shifting, bounded memory even at
        // high polling rates. 512 slots keep >500 ms of history even when a
        // 1000 Hz mouse reports every raw movement during a shake.
        private readonly Sample[] _ring = new Sample[512];
        private int _head;
        private int _count;
        private long _lastFeed = -1000;
        private long _cooldownUntil;

        public double WindowMs { get; set; }
        public int RequiredReversals { get; set; }
        public int MinSpan { get; set; }
        public double MinPath { get; set; }
        public int CooldownMs { get; set; }
        public int MinDurationMs { get; set; }
        public int MinReversalIntervalMs { get; set; }
        public double MaxReversalsPerSecond { get; set; }

        public ShakeDetector()
        {
            WindowMs = 1200;
            RequiredReversals = 6;
            MinSpan = 120;
            MinPath = 400;
            CooldownMs = 2500;
            MinDurationMs = 350;
            MinReversalIntervalMs = 60;
            MaxReversalsPerSecond = 15.0;
        }

        public bool Feed(int x, int y)
        {
            long now = AppClock.Milliseconds;

            // Cap the sample rate at ~125 Hz. The gesture does not need more,
            // and this keeps the ring buffer covering several seconds even on
            // a 1000-8000 Hz mouse, so a long shake is not truncated before it
            // can be recognized.
            if (now - _lastFeed < 8)
            {
                return false;
            }

            _lastFeed = now;

            int tail = (_head + _count) % _ring.Length;
            _ring[tail] = new Sample { X = x, Y = y, T = now };
            if (_count < _ring.Length) _count++;
            else _head = (_head + 1) % _ring.Length;

            while (_count > 0 && now - _ring[_head].T > WindowMs)
            {
                _head = (_head + 1) % _ring.Length;
                _count--;
            }

            if (now < _cooldownUntil || _count < RequiredReversals + 1) return false;
            if (!IsShake()) return false;

            _cooldownUntil = now + CooldownMs;
            _head = 0;
            _count = 0;
            return true;
        }

        private bool IsShake()
        {
            int revX = 0, revY = 0;
            int dirX = 0, dirY = 0;
            double path = 0;
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            long firstRevX = 0, lastRevX = 0;
            long firstRevY = 0, lastRevY = 0;
            long minGapX = long.MaxValue, minGapY = long.MaxValue;
            bool hasRevX = false, hasRevY = false;

            for (int k = 1; k < _count; k++)
            {
                Sample prev = _ring[(_head + k - 1) % _ring.Length];
                Sample cur = _ring[(_head + k) % _ring.Length];
                int dx = cur.X - prev.X;
                int dy = cur.Y - prev.Y;
                path += Math.Sqrt(dx * dx + dy * dy);

                int px = cur.X;
                int py = cur.Y;
                if (px < minX) minX = px;
                if (px > maxX) maxX = px;
                if (py < minY) minY = py;
                if (py > maxY) maxY = py;

                if (Math.Abs(dx) >= 6)
                {
                    int sx = Math.Sign(dx);
                    if (dirX != 0 && sx != dirX)
                    {
                        if (hasRevX)
                        {
                            long gap = cur.T - lastRevX;
                            if (gap < minGapX) minGapX = gap;
                        }
                        else
                        {
                            hasRevX = true;
                            firstRevX = cur.T;
                        }
                        lastRevX = cur.T;
                        revX++;
                    }
                    dirX = sx;
                }

                if (Math.Abs(dy) >= 6)
                {
                    int sy = Math.Sign(dy);
                    if (dirY != 0 && sy != dirY)
                    {
                        if (hasRevY)
                        {
                            long gap = cur.T - lastRevY;
                            if (gap < minGapY) minGapY = gap;
                        }
                        else
                        {
                            hasRevY = true;
                            firstRevY = cur.T;
                        }
                        lastRevY = cur.T;
                        revY++;
                    }
                    dirY = sy;
                }
            }

            int spanX = maxX - minX;
            int spanY = maxY - minY;

            bool horizontal = revX >= RequiredReversals && spanX > MinSpan
                && hasRevX && lastRevX - firstRevX >= MinDurationMs
                && minGapX >= MinReversalIntervalMs;
            bool vertical = revY >= RequiredReversals && spanY > MinSpan
                && hasRevY && lastRevY - firstRevY >= MinDurationMs
                && minGapY >= MinReversalIntervalMs;

            // Speed limit: a genuine hand shake is roughly 3–8 reversals per
            // second. Ignore sustained high-frequency jitter that still passes
            // the interval filter (e.g. a 30 ms alternating flick).
            if (horizontal)
            {
                double seconds = (lastRevX - firstRevX) / 1000.0;
                double rate = seconds > 0 ? (revX - 1) / seconds : double.MaxValue;
                if (rate > MaxReversalsPerSecond) horizontal = false;
            }
            if (vertical)
            {
                double seconds = (lastRevY - firstRevY) / 1000.0;
                double rate = seconds > 0 ? (revY - 1) / seconds : double.MaxValue;
                if (rate > MaxReversalsPerSecond) vertical = false;
            }

            return path > MinPath && (horizontal || vertical);
        }
    }
}
