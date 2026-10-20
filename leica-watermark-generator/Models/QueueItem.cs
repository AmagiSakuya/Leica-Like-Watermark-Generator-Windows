using System.IO;
using System.Windows.Media.Imaging;

namespace leica_watermark_generator.Models
{
    /// <summary>
    /// 队列里的一条图片引用：只记录路径，不复制、不移动原文件。
    /// </summary>
    public class QueueItem : Observable
    {
        private ExifInfo _exif = new();
        private BitmapSource? _thumbnail;
        private bool _isRendered;

        public QueueItem(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        public string FileName => Path.GetFileName(FilePath);

        public string FileNameWithoutExt => Path.GetFileNameWithoutExtension(FilePath);

        public string Directory => Path.GetDirectoryName(FilePath) ?? "";

        public ExifInfo Exif { get => _exif; set => Set(ref _exif, value); }

        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set => Set(ref _thumbnail, value);
        }

        /// <summary>本次会话是否已导出过</summary>
        public bool IsRendered { get => _isRendered; set => Set(ref _isRendered, value); }

        public string SizeText
        {
            get
            {
                try
                {
                    var len = new FileInfo(FilePath).Length;
                    return len > 1024 * 1024
                        ? $"{len / 1024.0 / 1024.0:0.0} MB"
                        : $"{len / 1024.0:0} KB";
                }
                catch { return ""; }
            }
        }

        public string MissingText => Exif.HasMissing ? "缺 EXIF" : "";
    }
}
