using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using leica_watermark_generator.Core;
using leica_watermark_generator.Models;

namespace leica_watermark_generator.ViewModels
{
    /// <summary>
    /// 主窗口的状态与业务动作。队列只保存图片引用，导出时才真正读原图。
    /// </summary>
    public class MainViewModel : Observable
    {
        private readonly List<INotifyPropertyChanged> _hooked = new();
        private readonly DispatcherTimer _refreshTimer;
        private ObservableCollection<TextLine>? _hookedLines;

        private QueueItem? _selectedItem;
        private Preset? _selectedPreset;
        private TextLine? _selectedLine;
        private BitmapSource? _previewPhoto;
        private BitmapSource? _stripSource;
        private List<TextHit> _hits = new();
        private string _status = "就绪";
        private string _hint = "";
        private bool _isBusy;
        private double _progress;
        private double _previewScale = 1;

        public MainViewModel()
        {
            Data = PresetStore.Load();
            Queue.CollectionChanged += OnQueueChanged;

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            _refreshTimer.Tick += (_, _) =>
            {
                _refreshTimer.Stop();
                RefreshStrip();
            };

            SelectedPreset = Data.Presets.FirstOrDefault(p => p.Name == Data.CurrentPreset) ?? Data.Presets.FirstOrDefault();

            if (SelectedPreset != null)
            {
                HookStyle(SelectedPreset.Style);
                SelectedLine = SelectedPreset.Style.Lines.FirstOrDefault();
            }

            Status = Queue.Count == 0 ? "把图片拖进来，或点「添加图片」" : "就绪";
        }

        #region 数据

        public PresetFile Data { get; }

        public ObservableCollection<QueueItem> Queue { get; } = new();

        public ObservableCollection<Preset> Presets => Data.Presets;

        public AppSettings Settings => Data.Settings;

        public string PresetPath => PresetStore.FilePath;

        public QueueItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (!Set(ref _selectedItem, value)) return;
                HookExif(value);
                LoadPreviewPhoto(value);
                Raise(nameof(ExifActionText));
                RefreshHitsAndStrip();
            }
        }

        public Preset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                var old = _selectedPreset;
                if (!Set(ref _selectedPreset, value)) return;

                HookStyle(value?.Style);
                HookDefaults(value?.Defaults);
                SelectedLine = value?.Style.Lines.FirstOrDefault();
                if (value != null) Data.CurrentPreset = value.Name;

                PushDefaultsToQueue();
                Raise(nameof(Style));
                Raise(nameof(ExifActionText));
                RefreshAll();
            }
        }

        public WatermarkStyle? Style => _selectedPreset?.Style;

        public TextLine? SelectedLine
        {
            get => _selectedLine;
            set => Set(ref _selectedLine, value);
        }

        /// <summary>当前图片的预览用缩略图</summary>
        public BitmapSource? PreviewPhoto
        {
            get => _previewPhoto;
            private set => Set(ref _previewPhoto, value);
        }

        /// <summary>渲染出来的水印条（基准尺寸）</summary>
        public BitmapSource? StripSource
        {
            get => _stripSource;
            private set => Set(ref _stripSource, value);
        }

        public IReadOnlyList<TextHit> Hits => _hits;

        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public string Hint
        {
            get => _hint;
            set => Set(ref _hint, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (!Set(ref _isBusy, value)) return;
                Raise(nameof(IsIdle));
                Raise(nameof(ExportText));
            }
        }

        public bool IsIdle => !_isBusy;

        public double Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        public double QualityValue
        {
            get => Settings.Quality;
            set
            {
                Settings.Quality = (long)Math.Round(Math.Clamp(value, 10, 100));
                Raise();
                Raise(nameof(QualityText));
            }
        }

        public string QualityText => Settings.Quality.ToString();

        public string QueueCountText => Queue.Count == 0 ? "" : $"{Queue.Count} 张";

        /// <summary>右下角导出按钮上的文字</summary>
        public string ExportText => IsBusy
            ? "正在导出…"
            : Queue.Count == 0 ? "导出" : $"导出全部 {Queue.Count} 张";

        /// <summary>预览坐标系 → 显示坐标系的缩放比</summary>
        public double PreviewScale
        {
            get => _previewScale;
            set => Set(ref _previewScale, value);
        }

        public string CanvasText => Style == null ? "" : $"画布 {Style.BaseWidth} × {Style.StripeHeight}";

        public event EventHandler? PreviewInvalidated;

        #endregion

        #region 队列

        public int AddFiles(IEnumerable<string> paths)
        {
            var allow = new[] { ".jpg", ".jpeg", ".png" };
            int added = 0;

            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                if (!allow.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
                if (Queue.Any(q => string.Equals(q.FilePath, path, StringComparison.OrdinalIgnoreCase))) continue;

                var item = new QueueItem(path);
                var exif = ExifService.Read(path);
                if (SelectedPreset != null) exif.PushPreset(SelectedPreset.Defaults);
                item.Exif = exif;
                item.Thumbnail = ExifService.LoadThumbnail(path, 120);
                Queue.Add(item);
                added++;
            }

            if (added > 0)
            {
                SelectedItem ??= Queue.FirstOrDefault();
                Status = $"已添加 {added} 张图片";
            }
            else if (paths.Any())
            {
                Status = "没有可添加的图片（支持 jpg / png）";
            }

            Raise(nameof(QueueCountText));
            return added;
        }

        public void AddFolder(string folder, bool recursive = true)
        {
            if (!Directory.Exists(folder)) return;
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(folder, "*.*", option)
                .Where(f => new[] { ".jpg", ".jpeg", ".png" }
                    .Contains(Path.GetExtension(f).ToLowerInvariant()));
            AddFiles(files);
        }

        public void RemoveSelected()
        {
            if (SelectedItem == null) return;
            int index = Queue.IndexOf(SelectedItem);
            Queue.Remove(SelectedItem);
            SelectedItem = Queue.Count == 0 ? null : Queue[Math.Clamp(index, 0, Queue.Count - 1)];
            Raise(nameof(QueueCountText));
            Status = "已从队列移除";
        }

        public void ClearQueue()
        {
            Queue.Clear();
            SelectedItem = null;
            Raise(nameof(QueueCountText));
            Status = "队列已清空";
        }

        private void OnQueueChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Raise(nameof(QueueCountText));
            Raise(nameof(ExportText));
            if (SelectedItem == null) SelectedItem = Queue.FirstOrDefault();
        }

        /// <summary>没有 EXIF 的图片：把预设里的默认 EXIF 填进去</summary>
        public void ApplyPresetExif()
        {
            if (SelectedItem == null || SelectedPreset == null) return;

            SelectedItem.Exif.PushPreset(SelectedPreset.Defaults);
            SelectedItem.Exif.ApplyPreset();
            RefreshStrip();
            Status = "已套用预设的默认 EXIF";
        }

        /// <summary>把当前图片的参数存成这个预设的默认 EXIF</summary>
        public void SaveCurrentAsDefaults()
        {
            if (SelectedItem == null || SelectedPreset == null) return;

            var defaults = SelectedPreset.Defaults;
            foreach (var field in SelectedItem.Exif.AllFields)
                defaults[field.Key] = field.Value;

            Status = "已把当前图片的参数存为预设默认值";
        }

        /// <summary>EXIF 面板上那个按钮的文案：没 EXIF 时改成读预设</summary>
        public string ExifActionText =>
            SelectedItem != null && !SelectedItem.Exif.HasAnyRaw ? "读取预设 EXIF" : "读取 EXIF";

        /// <summary>
        /// 重新从文件里读一遍 EXIF。读不到就是空，界面上手改的值会被覆盖。
        /// </summary>
        public void ReloadExif()
        {
            if (SelectedItem == null) return;

            var fresh = ExifService.Read(SelectedItem.FilePath);
            foreach (var field in SelectedItem.Exif.AllFields)
            {
                field.Raw = fresh[field.Key];
                field.Value = field.Raw;
            }

            RefreshStrip();
            Raise(nameof(ExifActionText));
            Status = SelectedItem.Exif.HasAnyRaw ? "已重新读取 EXIF" : "这张图没有 EXIF 信息";
        }

        #endregion

        #region 预设

        public Preset CreatePreset()
        {
            var preset = new Preset { Name = NextPresetName("新预设") };
            preset.Style.Logo.File = SelectedPreset?.Style.Logo.File ?? "img/Canon.png";
            Presets.Add(preset);
            SelectedPreset = preset;
            Save();
            return preset;
        }

        public Preset? DuplicatePreset()
        {
            if (SelectedPreset == null) return null;
            var copy = SelectedPreset.Clone(NextPresetName(SelectedPreset.Name + " 副本"));
            Presets.Add(copy);
            SelectedPreset = copy;
            Save();
            return copy;
        }

        public bool DeletePreset()
        {
            if (SelectedPreset == null || Presets.Count <= 1) return false;
            int index = Presets.IndexOf(SelectedPreset);
            Presets.Remove(SelectedPreset);
            SelectedPreset = Presets[Math.Clamp(index, 0, Presets.Count - 1)];
            Save();
            return true;
        }

        private string NextPresetName(string baseName)
        {
            if (!Presets.Any(p => p.Name == baseName)) return baseName;
            for (int i = 2; i < 1000; i++)
            {
                string name = $"{baseName} {i}";
                if (!Presets.Any(p => p.Name == name)) return name;
            }
            return baseName + " " + Guid.NewGuid().ToString("N")[..4];
        }

        public void Save()
        {
            try
            {
                PresetStore.Save(Data);
            }
            catch (Exception ex)
            {
                Status = "保存预设失败：" + ex.Message;
            }
        }

        #endregion

        #region 文字行

        public TextLine AddLine()
        {
            if (Style == null) return null!;
            var line = new TextLine
            {
                Name = "文字 " + (Style.Lines.Count + 1),
                Template = "{Model}",
                X = 205,
                Y = 82
            };
            Style.Lines.Add(line);
            SelectedLine = line;
            return line;
        }

        public void RemoveLine()
        {
            if (Style == null || SelectedLine == null) return;
            int index = Style.Lines.IndexOf(SelectedLine);
            Style.Lines.Remove(SelectedLine);
            SelectedLine = Style.Lines.Count == 0
                ? null
                : Style.Lines[Math.Clamp(index, 0, Style.Lines.Count - 1)];
        }

        public void MoveLine(int delta)
        {
            if (Style == null || SelectedLine == null) return;
            int index = Style.Lines.IndexOf(SelectedLine);
            int target = index + delta;
            if (target < 0 || target >= Style.Lines.Count) return;
            Style.Lines.Move(index, target);
        }

        #endregion

        #region 预览

        private void LoadPreviewPhoto(QueueItem? item)
        {
            PreviewPhoto = item == null ? null : ExifService.LoadThumbnail(item.FilePath, 1600);
        }

        /// <summary>重新渲染水印条（参数变了就调它）</summary>
        public void RefreshStrip()
        {
            if (Style == null || SelectedItem == null)
            {
                StripSource = null;
                _hits = new List<TextHit>();
                return;
            }

            try
            {
                var hits = new List<TextHit>();
                using var bmp = WatermarkRenderer.RenderStrip(Style, SelectedItem.Exif, hits);
                StripSource = WatermarkRenderer.ToBitmapSource(bmp);
                _hits = hits;
                PreviewInvalidated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StripSource = null;
                _hits = new List<TextHit>();
                Status = "预览渲染失败：" + ex.Message;
            }
        }

        private void RefreshHitsAndStrip()
        {
            RefreshStrip();
        }

        private void RefreshAll()
        {
            // 拖动/连续输入时合并重绘，避免每帧都渲染一张 4032 宽的位图
            _refreshTimer.Stop();
            _refreshTimer.Start();

            Raise(nameof(CanvasText));
        }

        #endregion

        #region 订阅

        private void HookStyle(WatermarkStyle? style)
        {
            foreach (var obj in _hooked) obj.PropertyChanged -= OnStylePropertyChanged;
            _hooked.Clear();

            if (_hookedLines != null)
            {
                _hookedLines.CollectionChanged -= OnLinesChanged;
                _hookedLines = null;
            }

            if (style == null) return;

            Hook(style);
            Hook(style.Separator);
            Hook(style.Logo);
            foreach (var line in style.Lines) Hook(line);

            _hookedLines = style.Lines;
            _hookedLines.CollectionChanged += OnLinesChanged;
        }

        private void Hook(INotifyPropertyChanged obj)
        {
            obj.PropertyChanged += OnStylePropertyChanged;
            _hooked.Add(obj);
        }

        private void OnStylePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextLine.X) || e.PropertyName == nameof(TextLine.Y))
                Raise(nameof(SelectedLine));
            RefreshAll();
        }

        private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TextLine line in e.NewItems) Hook(line);
            if (e.OldItems != null)
                foreach (TextLine line in e.OldItems)
                {
                    line.PropertyChanged -= OnStylePropertyChanged;
                    _hooked.Remove(line);
                }

            var style = Style;
            if (style == null) return;

            if (SelectedLine == null || !style.Lines.Contains(SelectedLine))
                SelectedLine = style.Lines.FirstOrDefault();

            RefreshAll();
        }

        private ExifDefaults? _hookedDefaults;

        private void HookDefaults(ExifDefaults? defaults)
        {
            if (_hookedDefaults != null) _hookedDefaults.PropertyChanged -= OnDefaultsChanged;
            _hookedDefaults = defaults;
            if (_hookedDefaults != null) _hookedDefaults.PropertyChanged += OnDefaultsChanged;
        }

        /// <summary>
        /// 改默认值时，只有「当前值本来就是预设值」的图片跟着变，
        /// 没套用过预设的图片不动。
        /// </summary>
        private void OnDefaultsChanged(object? sender, PropertyChangedEventArgs e)
        {
            string? key = e.PropertyName;
            if (string.IsNullOrEmpty(key) || SelectedPreset == null) return;

            var defaults = SelectedPreset.Defaults;

            foreach (var item in Queue)
            {
                var field = item.Exif.AllFields.FirstOrDefault(f => f.Key == key);
                if (field == null) continue;

                string old = field.PresetValue;
                bool tracking = !string.IsNullOrEmpty(old) && field.Value == old;

                field.PresetValue = defaults[key];
                if (tracking) field.Value = field.PresetValue;
            }
        }

        private void PushDefaultsToQueue()
        {
            if (SelectedPreset == null) return;
            foreach (var item in Queue) item.Exif.PushPreset(SelectedPreset.Defaults);
        }

        private void HookExif(QueueItem? item)
        {
            foreach (var q in Queue)
                foreach (var field in q.Exif.AllFields)
                    field.PropertyChanged -= OnExifChanged;

            if (item == null) return;
            foreach (var field in item.Exif.AllFields)
                field.PropertyChanged += OnExifChanged;
        }

        private void OnExifChanged(object? sender, PropertyChangedEventArgs e)
        {
            RefreshAll();
        }

        #endregion

        #region 导出

        /// <summary>导出队列里的全部图片，返回（成功数, 失败信息）</summary>
        public async Task<(int Success, List<string> Errors)> ExportAllAsync()
        {
            var errors = new List<string>();
            int success = 0;

            if (Style == null || Queue.Count == 0) return (0, errors);

            var dir = Settings.OutputDir;
            if (string.IsNullOrWhiteSpace(dir))
            {
                errors.Add("还没有选择导出目录");
                return (0, errors);
            }

            Directory.CreateDirectory(dir);
            IsBusy = true;
            Progress = 0;

            try
            {
                for (int i = 0; i < Queue.Count; i++)
                {
                    var item = Queue[i];
                    Status = $"正在导出 {i + 1}/{Queue.Count}：{item.FileName}";

                    try
                    {
                        string fileName = ExportService.BuildFileName(Settings.NamingPattern, item.Exif, item.FilePath);
                        string outPath = Path.Combine(dir, fileName);
                        if (!Settings.Overwrite) outPath = ExportService.EnsureUnique(outPath);

                        await Task.Run(() => WatermarkRenderer.Export(
                            Style, item.Exif, item.FilePath, outPath, Settings.Quality));

                        item.IsRendered = true;
                        success++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{item.FileName}：{ex.Message}");
                    }

                    Progress = (i + 1) * 100.0 / Queue.Count;
                }

                Status = errors.Count == 0
                    ? $"导出完成，共 {success} 张 → {dir}"
                    : $"导出完成 {success} 张，失败 {errors.Count} 张";
            }
            finally
            {
                IsBusy = false;
            }

            return (success, errors);
        }

        #endregion
    }
}
