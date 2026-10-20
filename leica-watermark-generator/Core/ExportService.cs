using System.IO;
using leica_watermark_generator.Models;

namespace leica_watermark_generator.Core
{
    /// <summary>导出文件名与批量处理</summary>
    public static class ExportService
    {
        public static string BuildFileName(string pattern, ExifInfo exif, string sourcePath)
        {
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string date = DateTime.TryParse(exif.DateTime, out var dt)
                ? dt.ToString("yyyyMMdd_HHmmss")
                : SafeLastWrite(sourcePath).ToString("yyyyMMdd_HHmmss");

            string result = string.IsNullOrWhiteSpace(pattern) ? "{filename}" : pattern;
            result = result
                .Replace("{filename}", name)
                .Replace("{date}", date)
                .Replace("{brand}", exif.Make)
                .Replace("{model}", exif.Model);

            foreach (var c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');

            result = result.Trim();
            if (string.IsNullOrWhiteSpace(result)) result = name;

            return result + ".jpg";
        }

        private static DateTime SafeLastWrite(string path)
        {
            try { return File.GetLastWriteTime(path); }
            catch { return DateTime.Now; }
        }

        /// <summary>目标文件重名时自动加序号</summary>
        public static string EnsureUnique(string path)
        {
            if (!File.Exists(path)) return path;

            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
        }
    }
}
