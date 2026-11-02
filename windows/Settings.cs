using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace FindMyCursor
{
    internal static class Settings
    {
        public static double Scale = 4.0;
        public static bool Enabled = true;
        public static bool Enlarge = true;

        // auto = use the colours sampled from the live cursor
        // fixed = use FillColor / OutlineColor below
        public static string ColorMode = "auto";
        public static Color FillColor = ColorTranslator.FromHtml("#E63741");
        public static Color OutlineColor = ColorTranslator.FromHtml("#2D1216");
        public static bool Ripple = true;

        // custom = our own red-arrow design; live = trace the current cursor
        public static string CursorStyle = "custom";

        // IME indicator: use the same red design for the normal pointer while
        // a Chinese IME is active.
        public static bool ImeIndicator = true;
        public static int ImeSize = 28;

        public static string ConfigPath
        {
            get { return FilePath; }
        }

        private static string FolderPath
        {
            get
            {
                // Keep settings next to the executable, together with the
                // InputCursorIndicator tool, instead of a separate AppData dir.
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.ini"); }
        }

        public static void EnsureFile()
        {
            try
            {
                if (!File.Exists(FilePath)) Save();
            }
            catch
            {
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                string[] lines = File.ReadAllLines(FilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (string.Equals(key, "scale", StringComparison.OrdinalIgnoreCase))
                    {
                        double parsed;
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                            && parsed >= 1.5 && parsed <= 8.0)
                        {
                            Scale = parsed;
                        }
                    }
                    else if (string.Equals(key, "enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        Enabled = value != "0";
                    }
                    else if (string.Equals(key, "enlarge", StringComparison.OrdinalIgnoreCase))
                    {
                        Enlarge = value != "0";
                    }
                    else if (string.Equals(key, "colorMode", StringComparison.OrdinalIgnoreCase))
                    {
                        ColorMode = string.Equals(value, "fixed", StringComparison.OrdinalIgnoreCase)
                            ? "fixed" : "auto";
                    }
                    else if (string.Equals(key, "fill", StringComparison.OrdinalIgnoreCase))
                    {
                        try { FillColor = ColorTranslator.FromHtml(value); }
                        catch { }
                    }
                    else if (string.Equals(key, "outline", StringComparison.OrdinalIgnoreCase))
                    {
                        try { OutlineColor = ColorTranslator.FromHtml(value); }
                        catch { }
                    }
                    else if (string.Equals(key, "ripple", StringComparison.OrdinalIgnoreCase))
                    {
                        Ripple = value != "0";
                    }
                    else if (string.Equals(key, "cursorStyle", StringComparison.OrdinalIgnoreCase))
                    {
                        CursorStyle = string.Equals(value, "live", StringComparison.OrdinalIgnoreCase)
                            ? "live" : "custom";
                    }
                    else if (string.Equals(key, "imeIndicator", StringComparison.OrdinalIgnoreCase))
                    {
                        ImeIndicator = value != "0";
                    }
                    else if (string.Equals(key, "imeSize", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                            && parsed >= 16 && parsed <= 64)
                        {
                            ImeSize = parsed;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(FilePath,
                    "# FindMyCursor settings" + Environment.NewLine +
                    "# colorMode: auto = follow the live cursor, fixed = use fill/outline" + Environment.NewLine +
                    "# fill/outline: #RRGGBB" + Environment.NewLine +
                    "scale=" + Scale.ToString("0.0", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "enabled=" + (Enabled ? "1" : "0") + Environment.NewLine +
                    "enlarge=" + (Enlarge ? "1" : "0") + Environment.NewLine +
                    "colorMode=" + ColorMode + Environment.NewLine +
                    "fill=" + Hex(FillColor) + Environment.NewLine +
                    "outline=" + Hex(OutlineColor) + Environment.NewLine +
                    "ripple=" + (Ripple ? "1" : "0") + Environment.NewLine +
                    "cursorStyle=" + CursorStyle + Environment.NewLine +
                    "imeIndicator=" + (ImeIndicator ? "1" : "0") + Environment.NewLine +
                    "imeSize=" + ImeSize.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string Hex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }
    }
}
