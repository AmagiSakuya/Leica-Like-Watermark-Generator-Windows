using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using leica_watermark_generator.Models;

namespace leica_watermark_generator.Converters
{
    /// <summary>null → Collapsed</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = value != null;
            if (Invert) hasValue = !hasValue;
            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>空字符串 → Collapsed</summary>
    public class TextToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasText = !string.IsNullOrWhiteSpace(value as string);
            if (Invert) hasText = !hasText;
            return hasText ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>文件路径 → 缩略图（Logo 预览用）</summary>
    public class PathToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string full = Core.WatermarkRenderer.ResolvePath(path);
                if (!System.IO.File.Exists(full)) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(full, UriKind.Absolute);
                bmp.DecodePixelWidth = 160;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>null → false（用于按钮 IsEnabled）</summary>
    public class NullToBoolConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = value != null;
            return Invert ? !hasValue : hasValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// EXIF 字段底色/边框：默认=文件里的 EXIF，蓝=预设兜底，橙=手动改过。
    /// </summary>
    public class FieldStateBrushConverter : IValueConverter
    {
        private static readonly Brush ExifBg = Freeze(0x20, 0x23, 0x29);
        private static readonly Brush ExifBorder = Freeze(0x26, 0x29, 0x2F);
        private static readonly Brush PresetBg = Freeze(0x15, 0x28, 0x3B);
        private static readonly Brush PresetBorder = Freeze(0x2E, 0x5B, 0x7E);
        private static readonly Brush ManualBg = Freeze(0x33, 0x28, 0x0F);
        private static readonly Brush ManualBorder = Freeze(0x7A, 0x5C, 0x1C);

        /// <summary>bg / border</summary>
        public string Kind { get; set; } = "bg";

        private static Brush Freeze(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var state = value is FieldState s ? s : FieldState.Exif;
            bool border = string.Equals(Kind, "border", StringComparison.OrdinalIgnoreCase);

            return state switch
            {
                FieldState.Preset => border ? PresetBorder : PresetBg,
                FieldState.Manual => border ? ManualBorder : ManualBg,
                _ => border ? ExifBorder : ExifBg
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>0..1 透明度 → 百分比文字</summary>
    public class PercentConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is double d ? $"{d * 100:0}%" : "";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
