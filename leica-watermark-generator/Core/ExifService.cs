using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using ExifLibrary;
using leica_watermark_generator.Models;

namespace leica_watermark_generator.Core
{
    /// <summary>
    /// 读取 EXIF。读不到的字段就是空，不猜、不用预设兜底，
    /// 需要的话用户在界面里直接填。
    /// </summary>
    public static class ExifService
    {
        public static ExifInfo Read(string path)
        {
            var info = new ExifInfo { FileName = Path.GetFileNameWithoutExtension(path) };

            try
            {
                var file = ImageFile.FromFile(path);

                info.SetRaw("Make", GetAscii(file, ExifTag.Make) ?? "");
                info.SetRaw("Model", GetAscii(file, ExifTag.Model) ?? "");
                info.SetRaw("LensModel", GetAscii(file, ExifTag.LensModel) ?? "");

                string focal = "";
                if (file.Properties.Get<ExifURational>(ExifTag.FocalLength) is { } f && f.Value.Denominator != 0)
                {
                    double value = (double)f.Value.Numerator / f.Value.Denominator;
                    focal = ((int)Math.Round(value)).ToString();
                }
                info.SetRaw("FocalLength", focal);

                info.SetRaw("ShutterSpeedValue",
                    ConvertApexShutterSpeed(file.Properties.Get<ExifSRational>(ExifTag.ShutterSpeedValue)));
                info.SetRaw("ApertureValue",
                    ConvertApexAperture(file.Properties.Get<ExifURational>(ExifTag.ApertureValue)));

                var iso = file.Properties.Get<ExifUShort>(ExifTag.ISOSpeedRatings);
                info.SetRaw("ISO", iso?.Value != null ? iso.Value.ToString() : "");

                var date = file.Properties.Get<ExifDateTime>(ExifTag.DateTime);
                info.SetRaw("DateTime", date?.Value != null ? date.Value.ToString("yyyy/MM/dd HH:mm:ss") : "");
            }
            catch
            {
                // 完全没有 EXIF 的图片，所有字段保持为空
            }

            return info;
        }

        private static string? GetAscii(ImageFile file, ExifTag tag)
        {
            var prop = file.Properties.Get<ExifAscii>(tag);
            if (string.IsNullOrWhiteSpace(prop?.Value)) return null;
            return prop.Value.Trim();
        }

        /// <summary>APEX 快门速度 → 1/125″ 或 1.5″</summary>
        private static string ConvertApexShutterSpeed(ExifSRational? value)
        {
            if (value == null || value.Value.Denominator == 0) return "";

            double apex = (double)value.Value.Numerator / value.Value.Denominator;
            double shutter = Math.Pow(2, apex);
            return shutter < 1
                ? $"{shutter:0.##}″"
                : $"1/{Math.Round(shutter)}″";
        }

        /// <summary>APEX 光圈 → f/2.8</summary>
        private static string ConvertApexAperture(ExifURational? value)
        {
            if (value == null || value.Value.Denominator == 0) return "";

            double apex = (double)value.Value.Numerator / value.Value.Denominator;
            double aperture = Math.Pow(Math.Sqrt(2), apex);
            return "f/" + aperture.ToString("0.0");
        }

        /// <summary>读缩略图（预览用，按宽度解码，省内存）</summary>
        public static BitmapSource? LoadThumbnail(string path, int decodeWidth = 1400)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.DecodePixelWidth = decodeWidth;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>读取图片像素尺寸</summary>
        public static (int Width, int Height) GetSize(string path)
        {
            try
            {
                using var img = Image.FromFile(path);
                return (img.Width, img.Height);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// 按 EXIF Orientation 校正方向后加载原图（导出用），
        /// 与 WPF 预览的显示方向保持一致。
        /// </summary>
        public static Bitmap LoadOriented(string path)
        {
            var bmp = new Bitmap(Image.FromFile(path));
            try
            {
                var file = ImageFile.FromFile(path);
                var orientation = file.Properties.Get<ExifUShort>(ExifTag.Orientation);
                int value = orientation?.Value ?? 1;

                RotateFlipType? transform = value switch
                {
                    2 => RotateFlipType.RotateNoneFlipX,
                    3 => RotateFlipType.Rotate180FlipNone,
                    4 => RotateFlipType.Rotate180FlipX,
                    5 => RotateFlipType.Rotate90FlipX,
                    6 => RotateFlipType.Rotate90FlipNone,
                    7 => RotateFlipType.Rotate270FlipX,
                    8 => RotateFlipType.Rotate270FlipNone,
                    _ => null
                };

                if (transform.HasValue) bmp.RotateFlip(transform.Value);
            }
            catch
            {
                // 没有 Orientation 就按原样
            }

            return bmp;
        }
    }
}
