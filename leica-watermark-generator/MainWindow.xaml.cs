using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using leica_watermark_generator.Models;
using leica_watermark_generator.ViewModels;
using leica_watermark_generator.Views;

namespace leica_watermark_generator
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();

        private TextBox? _tokenTarget;
        private TextLine? _dragLine;
        private Point _dragStart;
        private double _dragStartX, _dragStartY;
        private bool _dragging;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            _vm.PreviewInvalidated += (_, _) => UpdatePreview();

            Loaded += (_, _) => UpdatePreview();
            Closing += (_, _) => _vm.Save();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        #region 自绘窗体

        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        }

        /// <summary>
        /// 无边框窗口最大化会盖住任务栏，这里把最大化范围限制在工作区内。
        /// </summary>
        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

            var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, 0x02 /* MONITOR_DEFAULTTONEAREST */);

            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var work = monitorInfo.rcWork;
                    var screen = monitorInfo.rcMonitor;

                    info.ptMaxPosition.x = Math.Abs(work.left - screen.left);
                    info.ptMaxPosition.y = Math.Abs(work.top - screen.top);
                    info.ptMaxSize.x = Math.Abs(work.right - work.left);
                    info.ptMaxSize.y = Math.Abs(work.bottom - work.top);
                    info.ptMaxTrackSize.x = info.ptMaxSize.x;
                    info.ptMaxTrackSize.y = info.ptMaxSize.y;
                }
            }

            Marshal.StructureToPtr(info, lParam, true);
            handled = true;
            return IntPtr.Zero;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            if (MaximizeButton != null)
            {
                MaxIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
                RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
                MaximizeButton.ToolTip = maximized ? "还原" : "最大化";
            }

            if (RootBorder != null)
            {
                RootBorder.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
            }
        }

        #endregion

        #region 预览

        /// <summary>开发用：直接塞一张图进队列，便于截图检查</summary>
        internal void DebugLoadImages(IEnumerable<string> paths)
        {
            _vm.AddFiles(paths);
            _vm.SelectedItem = _vm.Queue.FirstOrDefault();
            UpdatePreview();
        }

        internal void DebugSelectTab(int index)
        {
            if (index >= 0 && index < ParamTabs.Items.Count) ParamTabs.SelectedIndex = index;
        }

        internal void DebugSelectIndex(int index)
        {
            _vm.SelectedItem = _vm.Queue.ElementAtOrDefault(index);
            UpdatePreview();
        }

        internal string DebugPreviewInfo()
        {
            var p = _vm.PreviewPhoto;
            string size = p == null ? "null" : $"{p.PixelWidth}x{p.PixelHeight}";
            return $"{size} orient={_vm.SelectedItem?.Exif.Orientation}";
        }

        internal void DebugFillDefaults()
        {
            _vm.SaveCurrentAsDefaults();
        }

        internal void DebugApplyPresetExif()
        {
            _vm.ApplyPresetExif();
            UpdatePreview();
        }

        internal void DebugSetField(string key, string value)
        {
            if (_vm.SelectedItem != null) _vm.SelectedItem.Exif[key] = value;
        }

        private void Stage_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (_vm.SelectedItem == null || _vm.PreviewPhoto == null || _vm.StripSource == null || _vm.Style == null)
            {
                Frame.Visibility = Visibility.Collapsed;
                return;
            }

            Frame.Visibility = Visibility.Visible;

            double availableW = StageHost.ActualWidth - 72;
            double availableH = StageHost.ActualHeight - 72;
            if (availableW < 60 || availableH < 60) return;

            double photoRatio = _vm.PreviewPhoto.PixelWidth / (double)_vm.PreviewPhoto.PixelHeight;
            double stripRatio = _vm.Style.StripeHeight / (double)_vm.Style.BaseWidth;

            double width = Math.Min(availableW, availableH / (1 / photoRatio + stripRatio));
            width = Math.Max(160, width);

            double photoHeight = width / photoRatio;
            double stripHeight = width * stripRatio;

            PhotoImage.Width = width;
            PhotoImage.Height = photoHeight;
            StripHost.Width = width;
            StripHost.Height = stripHeight;
            StripImage.Width = width;
            StripImage.Height = stripHeight;

            _vm.PreviewScale = width / _vm.Style.BaseWidth;
            UpdateSelectionBox();
        }

        private void UpdateSelectionBox()
        {
            if (_vm.Style == null || _vm.SelectedLine == null || _vm.PreviewScale <= 0)
            {
                SelBox.Visibility = Visibility.Collapsed;
                return;
            }

            int index = _vm.Style.Lines.IndexOf(_vm.SelectedLine);
            var hit = _vm.Hits.FirstOrDefault(h => h.Index == index);
            if (index < 0 || hit == null)
            {
                SelBox.Visibility = Visibility.Collapsed;
                return;
            }

            double s = _vm.PreviewScale;
            SelBox.Margin = new Thickness(hit.Bounds.X * s, hit.Bounds.Y * s, 0, 0);
            SelBox.Width = Math.Max(6, hit.Bounds.Width * s);
            SelBox.Height = Math.Max(6, hit.Bounds.Height * s);
            SelBox.Visibility = Visibility.Visible;
        }

        private void Strip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm.Style == null || _vm.SelectedItem == null || _vm.PreviewScale <= 0) return;

            var point = e.GetPosition(StripHost);
            double s = _vm.PreviewScale;
            var model = new Point(point.X / s, point.Y / s);

            var hit = _vm.Hits.LastOrDefault(h => h.Bounds.Contains((float)model.X, (float)model.Y));
            if (hit == null)
            {
                SelBox.Visibility = Visibility.Collapsed;
                return;
            }

            var line = _vm.Style.Lines[hit.Index];
            _vm.SelectedLine = line;

            _dragLine = line;
            _dragStart = point;
            _dragStartX = line.X;
            _dragStartY = line.Y;
            _dragging = true;

            StripHost.CaptureMouse();
            StripHost.Cursor = Cursors.SizeAll;
            UpdateSelectionBox();
            e.Handled = true;
        }

        private void Strip_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _dragLine == null || _vm.PreviewScale <= 0)
            {
                if (StripHost.IsMouseOver && _vm.PreviewScale > 0)
                {
                    var p = e.GetPosition(StripHost);
                    var m = new Point(p.X / _vm.PreviewScale, p.Y / _vm.PreviewScale);
                    StripHost.Cursor = _vm.Hits.Any(h => h.Bounds.Contains((float)m.X, (float)m.Y))
                        ? Cursors.SizeAll
                        : Cursors.Arrow;
                }
                return;
            }

            double s = _vm.PreviewScale;
            var now = e.GetPosition(StripHost);
            _dragLine.X = Math.Round(_dragStartX + (now.X - _dragStart.X) / s);
            _dragLine.Y = Math.Round(_dragStartY + (now.Y - _dragStart.Y) / s);
            UpdateSelectionBox();
        }

        private void Strip_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            _dragLine = null;
            StripHost.ReleaseMouseCapture();
            StripHost.Cursor = Cursors.Arrow;
            UpdateSelectionBox();
        }

        private void Strip_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_dragging) StripHost.Cursor = Cursors.Arrow;
        }

        #endregion

        #region 队列

        private void AddImages_Click(object sender, RoutedEventArgs e) => AddImages();

        private void AddImages()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择图片",
                Multiselect = true,
                Filter = "图片 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                _vm.AddFiles(dialog.FileNames);
                UpdatePreview();
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择图片文件夹（含子目录）" };
            if (dialog.ShowDialog(this) == true)
            {
                _vm.AddFolder(dialog.FolderName);
                UpdatePreview();
            }
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            _vm.RemoveSelected();
            UpdatePreview();
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Queue.Count == 0) return;

            if (!AppDialog.Show(this, "清空队列",
                    $"队列里的 {_vm.Queue.Count} 张图片会全部移除（只移除引用，不会删除原文件）。",
                    "清空", true, true))
            {
                return;
            }

            _vm.ClearQueue();
            UpdatePreview();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

            var files = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path)) _vm.AddFolder(path);
                else files.Add(path);
            }

            if (files.Count > 0) _vm.AddFiles(files);
            UpdatePreview();
        }

        /// <summary>有 EXIF 就重读文件，没有就套用预设的默认 EXIF</summary>
        private void ExifAction_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItem == null) return;

            if (_vm.SelectedItem.Exif.HasAnyRaw)
                _vm.ReloadExif();
            else
                _vm.ApplyPresetExif();

            UpdatePreview();
        }

        private void SaveDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItem == null)
            {
                AppDialog.Info(this, "没有可用的图片", "先从队列里选一张图片，再把它填成默认值。");
                return;
            }

            _vm.SaveCurrentAsDefaults();
        }

        private void ClearDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedPreset == null) return;

            if (!AppDialog.Show(this, "清空默认 EXIF",
                    $"预设「{_vm.SelectedPreset.Name}」里的默认 EXIF 会被清空，已经套用过的图片不受影响。",
                    "清空", true, true))
                return;

            var defaults = _vm.SelectedPreset.Defaults;
            foreach (var key in new[] { "Make", "Model", "LensModel", "FocalLength",
                                        "ApertureValue", "ShutterSpeedValue", "ISO", "DateTime" })
            {
                defaults[key] = "";
            }

            _vm.Status = "已清空默认 EXIF";
        }

        #endregion

        #region 预设

        private void NewPreset_Click(object sender, RoutedEventArgs e)
        {
            _vm.CreatePreset();
            UpdatePreview();
        }

        private void DuplicatePreset_Click(object sender, RoutedEventArgs e)
        {
            _vm.DuplicatePreset();
            UpdatePreview();
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Presets.Count <= 1)
            {
                AppDialog.Info(this, "无法删除", "至少要保留一个预设。");
                return;
            }

            string name = _vm.SelectedPreset?.Name ?? "";
            if (!AppDialog.Show(this, "删除预设", $"确定要删除预设「{name}」吗？这个操作不可撤销。", "删除", true, true))
                return;

            _vm.DeletePreset();
            UpdatePreview();
        }

        #endregion

        #region 文字行

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            _vm.AddLine();
            UpdatePreview();
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            _vm.RemoveLine();
            UpdatePreview();
        }

        private void MoveLineUp_Click(object sender, RoutedEventArgs e)
        {
            _vm.MoveLine(-1);
            UpdatePreview();
        }

        private void MoveLineDown_Click(object sender, RoutedEventArgs e)
        {
            _vm.MoveLine(1);
            UpdatePreview();
        }

        private void TemplateBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OriginalSource is TextBox box) _tokenTarget = box;
        }

        private void Token_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Content is not string token) return;

            var box = _tokenTarget ?? TemplateBox;
            int caret = box.CaretIndex;
            string text = box.Text ?? "";
            box.Text = text.Insert(Math.Clamp(caret, 0, text.Length), token);
            box.CaretIndex = Math.Clamp(caret, 0, text.Length) + token.Length;
            box.Focus();
        }

        private void NameToken_Click(object sender, RoutedEventArgs e)
        {
            _tokenTarget = NamingBox;
            Token_Click(sender, e);
        }

        #endregion

        #region 颜色

        private void LineColor_Click(object sender, RoutedEventArgs e) => SetColor(sender, color => _vm.SelectedLine!.Color = color);

        private void SepColor_Click(object sender, RoutedEventArgs e)
            => SetColor(sender, color => { if (_vm.Style != null) _vm.Style.Separator.Color = color; });

        private void BgColor_Click(object sender, RoutedEventArgs e)
            => SetColor(sender, color => { if (_vm.Style != null) _vm.Style.Background = color; });

        private void SetColor(object sender, Action<string> apply)
        {
            if (sender is not Button { Tag: string color }) return;
            apply(color);
            UpdatePreview();
        }

        #endregion

        #region 图标 / 输出

        private void BrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Style == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 Logo（PNG，建议带透明通道）",
                Filter = "PNG 图片 (*.png)|*.png",
                InitialDirectory = Path.Combine(AppContext.BaseDirectory, "img")
            };

            if (dialog.ShowDialog(this) != true) return;

            string selected = dialog.FileName;
            string baseDir = AppContext.BaseDirectory;
            if (selected.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                selected = Path.GetRelativePath(baseDir, selected).Replace('\\', '/');

            _vm.Style.Logo.File = selected;
            UpdatePreview();
        }

        private void OpenLogoFolder_Click(object sender, RoutedEventArgs e)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "img");
            if (!Directory.Exists(dir))
            {
                AppDialog.Info(this, "提示", "程序目录下还没有 img 文件夹。");
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            if (PickOutputDir()) _vm.Save();
        }

        private bool PickOutputDir()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择导出目录",
                Multiselect = false
            };

            if (Directory.Exists(_vm.Settings.OutputDir)) dialog.InitialDirectory = _vm.Settings.OutputDir;
            if (dialog.ShowDialog(this) != true) return false;

            _vm.Settings.OutputDir = dialog.FolderName;
            return true;
        }

        private void OpenOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_vm.Settings.OutputDir))
            {
                AppDialog.Info(this, "提示", "导出目录还不存在，先选择或导出一次。");
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_vm.Settings.OutputDir}\"") { UseShellExecute = true });
        }

        #endregion

        #region 导出

        private async void Export_Click(object sender, RoutedEventArgs e) => await DoExportAsync();

        private async Task DoExportAsync()
        {
            if (_vm.IsBusy) return;

            if (_vm.Queue.Count == 0)
            {
                AppDialog.Info(this, "队列是空的", "先把图片添加到队列，再导出。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_vm.Settings.OutputDir) && !PickOutputDir()) return;

            var (success, errors) = await _vm.ExportAllAsync();
            _vm.Save();

            if (errors.Count > 0)
            {
                string message = string.Join(Environment.NewLine, errors.Take(8));
                if (errors.Count > 8) message += $"{Environment.NewLine}… 还有 {errors.Count - 8} 条";
                AppDialog.Error(this, $"有 {errors.Count} 张没导出成功", message);
                return;
            }

            if (success == 0) return;

            if (_vm.Settings.OpenAfterExport && Directory.Exists(_vm.Settings.OutputDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_vm.Settings.OutputDir}\"") { UseShellExecute = true });
            }
            else
            {
                AppDialog.Info(this, "导出完成", $"已导出 {success} 张图片到：{Environment.NewLine}{_vm.Settings.OutputDir}");
            }
        }

        #endregion

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                AddImages();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E)
            {
                _ = DoExportAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
            {
                _vm.RemoveSelected();
                UpdatePreview();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SelBox.Visibility = Visibility.Collapsed;
            }
        }
    }
}
