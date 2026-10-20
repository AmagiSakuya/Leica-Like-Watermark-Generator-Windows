using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using leica_watermark_generator.Models;
using leica_watermark_generator.Properties;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Drawing.Size;

namespace leica_watermark_generator.Core
{
    /// <summary>渲染出来的文字块位置（用于预览里拖动命中）</summary>
    public class TextHit
    {
        public int Index { get; set; }
        public RectangleF Bounds { get; set; }
    }

    /// <summary>
    /// 水印绘制。所有尺寸都基于样式里的基准坐标系（默认 4032 宽），
    /// 导出时按原图宽度等比放大，预览时等比缩小。
    /// </summary>
    public static class WatermarkRenderer
    {
        public const string BuiltinFont = "PingFang SC Medium";

        private static PrivateFontCollection? _privateFonts;
        private static readonly object _fontLock = new();

        private static PrivateFontCollection PrivateFonts
        {
            get
            {
                lock (_fontLock)
                {
                    if (_privateFonts == null)
                    {
                        var pfc = new PrivateFontCollection();
                        try
                        {
                            var data = Resources.PingFangSC_Medium;
                            if (data != null && data.Length > 0)
                            {
                                IntPtr ptr = Marshal.AllocCoTaskMem(data.Length);
                                try
                                {
                                    Marshal.Copy(data, 0, ptr, data.Length);
                                    pfc.AddMemoryFont(ptr, data.Length);
                                }
                                finally
                                {
                                    Marshal.FreeCoTaskMem(ptr);
                                }
                            }
                        }
                        catch
                        {
                            // 字体加载失败时退回系统字体
                        }
                        _privateFonts = pfc;
                    }
                    return _privateFonts;
                }
            }
        }

        public static string[] AvailableFonts => new[]
        {
            BuiltinFont, "Microsoft YaHei", "Microsoft YaHei UI", "SimSun", "SimHei", "Arial", "Segoe UI", "Times New Roman"
        };

        private static FontFamily ResolveFamily(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = BuiltinFont;
            if (string.Equals(name, BuiltinFont, StringComparison.OrdinalIgnoreCase))
            {
                var fams = PrivateFonts.Families;
                if (fams.Length > 0) return fams[0];
            }
            try
            {
                return new FontFamily(name);
            }
            catch
            {
                return System.Drawing.FontFamily.GenericSansSerif;
            }
        }

        /// <summary>
        /// 把模板里的 {xxx} 替换成实际值。
        /// 读不到的字段替换成空，并且连它旁边的单位（mm / ISO）一起吃掉，
        /// 免得出现 " mm  ISO" 这种残句。
        /// </summary>
        public static string Resolve(string template, ExifInfo exif, double focalMultiplier)
        {
            if (string.IsNullOrEmpty(template)) return "";

            string focal = exif.FocalLength;
            if (focalMultiplier > 0 && Math.Abs(focalMultiplier - 1.0) > 0.001
                && double.TryParse(focal, out double f))
            {
                focal = ((int)Math.Ceiling(f * focalMultiplier)).ToString();
            }

            string date = "", time = "";
            if (DateTime.TryParse(exif.DateTime, out var dt))
            {
                date = dt.ToString("yyyy/MM/dd");
                time = dt.ToString("HH:mm:ss");
            }

            var sb = new System.Text.StringBuilder(template.Length + 16);
            int i = 0;

            while (i < template.Length)
            {
                if (template[i] == '{')
                {
                    int close = template.IndexOf('}', i + 1);
                    if (close > i + 1)
                    {
                        string key = template[(i + 1)..close];
                        string value = TokenValue(key, exif, focal, date, time);

                        if (value.Length > 0)
                        {
                            sb.Append(value);
                            i = close + 1;
                            continue;
                        }

                        i = close + 1;

                        // 空值：吞掉紧跟在后面的单位（{FocalLength}mm）
                        foreach (var unit in new[] { "mm", "‰", "″", "s" })
                        {
                            if (string.CompareOrdinal(template, i, unit, 0, unit.Length) == 0)
                            {
                                i += unit.Length;
                                break;
                            }
                        }

                        // 空值：吞掉紧挨在前面的前缀（ISO{ISO}）
                        if (sb.Length >= 3 && sb.ToString(sb.Length - 3, 3) == "ISO")
                            sb.Length -= 3;

                        continue;
                    }
                }

                sb.Append(template[i]);
                i++;
            }

            string text = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();

            // 只剩空格和符号（例如全字段为空）就当这行没有内容
            return text.Any(char.IsLetterOrDigit) ? text : "";
        }

        private static string TokenValue(string key, ExifInfo exif, string focal, string date, string time) => key switch
        {
            "Model" => exif.Model,
            "Make" or "Brand" => exif.Make,
            "LensModel" or "Lens" => exif.LensModel,
            "FocalLength" => focal,
            "Aperture" => exif.ApertureValue,
            "Shutter" => exif.ShutterSpeedValue,
            "ISO" => exif.ISO,
            "DateTime" => exif.DateTime,
            "Date" => date,
            "Time" => time,
            "FileName" => exif.FileName,
            _ => ""
        };

        /// <summary>
        /// 按基准尺寸渲染整条水印。
        /// </summary>
        public static Bitmap RenderStrip(WatermarkStyle style, ExifInfo exif, List<TextHit>? hits = null)
        {
            int w = Math.Max(16, style.BaseWidth);
            int h = Math.Max(16, style.StripeHeight);

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            bmp.SetResolution(96, 96);

            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                g.Clear(ParseColor(style.Background, Color.White));

                // 分隔线
                if (style.Separator is { Enabled: true, Width: > 0 })
                {
                    var sep = style.Separator;
                    using var pen = new Pen(ParseColor(sep.Color, Color.Gray), (float)sep.Width);
                    g.DrawLine(pen, (float)sep.X, (float)sep.Y1, (float)sep.X, (float)sep.Y2);
                }

                // Logo
                DrawLogo(g, style, bmp.Width, bmp.Height);

                // 文字
                var format = (StringFormat)StringFormat.GenericTypographic.Clone();
                format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                format.Trimming = StringTrimming.None;

                for (int i = 0; i < style.Lines.Count; i++)
                {
                    var line = style.Lines[i];
                    if (!line.Enabled) continue;

                    string text = Resolve(line.Template, exif, style.FocalLengthMultiplier);
                    if (string.IsNullOrEmpty(text)) continue;

                    float size = (float)Math.Max(1, line.Size);
                    var family = ResolveFamily(line.FontFamily);

                    using var font = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
                    var pos = new PointF((float)line.X, (float)line.Y);
                    var textColor = ParseColor(line.Color, Color.Black);
                    if (line.Opacity < 1)
                        textColor = Color.FromArgb((int)Math.Round(255 * Math.Clamp(line.Opacity, 0, 1)), textColor);

                    // 描边
                    if (line.StrokeWidth > 0)
                    {
                        using var path = new GraphicsPath();
                        path.AddString(text, family, (int)FontStyle.Regular, size, pos, format);
                        using var pen = new Pen(ParseColor(line.StrokeColor, Color.Black), (float)line.StrokeWidth)
                        {
                            LineJoin = LineJoin.Round
                        };
                        g.DrawPath(pen, path);
                    }

                    using (var brush = new SolidBrush(textColor))
                    {
                        g.DrawString(text, font, brush, pos, format);
                    }

                    hits?.Add(new TextHit
                    {
                        Index = i,
                        Bounds = new RectangleF(pos.X, pos.Y, Math.Max(1, size), Math.Max(1, size))
                    });
                }
            }

            // 命中区域按实际文字尺寸重算
            if (hits is { Count: > 0 })
            {
                using var g = Graphics.FromImage(bmp);
                var format = StringFormat.GenericTypographic;
                foreach (var hit in hits)
                {
                    var line = style.Lines[hit.Index];
                    var family = ResolveFamily(line.FontFamily);
                    using var font = new Font(family, (float)Math.Max(1, line.Size), FontStyle.Regular, GraphicsUnit.Pixel);
                    string text = Resolve(line.Template, exif, style.FocalLengthMultiplier);
                    var sz = g.MeasureString(text, font, PointF.Empty, format);
                    hit.Bounds = new RectangleF(
                        (float)line.X - 4,
                        (float)line.Y - 4,
                        (float)Math.Max(8, sz.Width) + 8,
                        (float)Math.Max(8, sz.Height) + 8);
                }
            }

            return bmp;
        }

        private static void DrawLogo(Graphics g, WatermarkStyle style, int canvasW, int canvasH)
        {
            var logo = style.Logo;
            if (logo is not { Enabled: true } || string.IsNullOrWhiteSpace(logo.File)) return;

            string path = ResolvePath(logo.File);
            if (!File.Exists(path)) return;

            try
            {
                using var src = new Bitmap(path);
                float targetW = (float)Math.Max(1, logo.Width);
                float scale = targetW / src.Width;
                float targetH = src.Height * scale;

                var dest = new RectangleF((float)logo.X, (float)logo.Y, targetW, targetH);
                if (logo.Opacity >= 0.999)
                {
                    g.DrawImage(src, dest);
                }
                else
                {
                    using var attrs = new ImageAttributes();
                    var cm = new ColorMatrix { Matrix33 = (float)Math.Clamp(logo.Opacity, 0, 1) };
                    attrs.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(src, Rectangle.Round(dest), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
                }
            }
            catch
            {
                // Logo 读不出来就跳过，不影响其它元素
            }
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(AppContext.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
        }

        public static Color ParseColor(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            try
            {
                var s = value.Trim();
                if (s.StartsWith("#")) s = s.Substring(1);
                if (s.Length == 3)
                {
                    s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
                }
                if (s.Length == 6 || s.Length == 8)
                {
                    uint v = Convert.ToUInt32(s, 16);
                    if (s.Length == 6)
                        return Color.FromArgb(255, (int)((v >> 16) & 0xFF), (int)((v >> 8) & 0xFF), (int)(v & 0xFF));
                    return Color.FromArgb((int)((v >> 24) & 0xFF), (int)((v >> 16) & 0xFF), (int)((v >> 8) & 0xFF), (int)(v & 0xFF));
                }
            }
            catch
            {
                // ignore
            }
            return fallback;
        }

        /// <summary>转成 WPF 可显示的位图（预览用）</summary>
        public static BitmapSource ToBitmapSource(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var source = BitmapSource.Create(
                    bmp.Width, bmp.Height, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null,
                    data.Scan0, data.Stride * bmp.Height, data.Stride);
                source.Freeze();
                return source;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        /// <summary>
        /// 导出单张图片：原图 + 底部水印条。
        /// </summary>
        public static void Export(WatermarkStyle style, ExifInfo exif, string sourcePath, string outputPath, long quality)
        {
            using var strip = RenderStrip(style, exif);
            using var original = ExifService.LoadOriented(sourcePath);

            float scale = (float)original.Width / strip.Width;
            int sw = Math.Max(1, (int)Math.Round(strip.Width * scale));
            int sh = Math.Max(1, (int)Math.Round(strip.Height * scale));

            using var scaledStrip = new Bitmap(strip, new Size(sw, sh));

            using var canvas = new Bitmap(original.Width, original.Height + sh, PixelFormat.Format24bppRgb);
            try
            {
                if (original.HorizontalResolution > 0 && original.VerticalResolution > 0)
                    canvas.SetResolution(original.HorizontalResolution, original.VerticalResolution);
            }
            catch { /* 分辨率异常时用默认值 */ }

            using (var g = Graphics.FromImage(canvas))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.White);
                g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height));
                g.DrawImage(scaledStrip, new Rectangle(0, original.Height, sw, sh));
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var encoder = GetEncoder(ImageFormat.Jpeg);
            if (encoder == null)
            {
                canvas.Save(outputPath, ImageFormat.Jpeg);
                return;
            }

            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Math.Clamp(quality, 10, 100));
            canvas.Save(outputPath, encoder, parameters);
        }

        private static ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == format.Guid);
        }
    }
}
