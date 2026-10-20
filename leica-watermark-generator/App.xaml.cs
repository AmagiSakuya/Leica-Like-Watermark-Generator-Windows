using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using leica_watermark_generator.Core;
using leica_watermark_generator.Models;

namespace leica_watermark_generator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnUnhandledException;

            if (e.Args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                RunSelfTest();
                return;
            }

            if (e.Args.Any(a => a.Equals("--shot", StringComparison.OrdinalIgnoreCase)))
            {
                int tab = 0;
                int pos = Array.FindIndex(e.Args, a => a.Equals("--shot", StringComparison.OrdinalIgnoreCase));
                if (pos >= 0 && e.Args.Length > pos + 1) int.TryParse(e.Args[pos + 1], out tab);
                RunScreenshot(tab,
                    e.Args.Any(a => a.Equals("max", StringComparison.OrdinalIgnoreCase)),
                    e.Args.Any(a => a.Equals("plain", StringComparison.OrdinalIgnoreCase)),
                    e.Args.Any(a => a.Equals("fill", StringComparison.OrdinalIgnoreCase)));
                return;
            }

            var window = new MainWindow();
            window.Show();
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(AppContext.BaseDirectory, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // 日志写不进去就算了
            }

            MessageBox.Show(e.Exception.Message, "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        /// <summary>
        /// 开发用：把窗口渲染成 PNG，方便检查布局（不参与正常使用）
        /// </summary>
        private void RunScreenshot(int tab, bool maximize, bool plain = false, bool fill = false)
        {
            var window = new MainWindow { Width = 1440, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual, Left = 20, Top = 20 };
            window.Show();
            window.DebugSelectTab(tab);
            if (maximize) window.WindowState = WindowState.Maximized;

            window.Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(500);

                string demo = Path.Combine(AppContext.BaseDirectory, "UserData", "Input", "Canon", "demo.jpg");
                if (File.Exists(demo))
                {
                    if (plain)
                    {
                        // 再造一张没有 EXIF 的图，检查「读取预设 EXIF」这套流程
                        string bare = Path.Combine(AppContext.BaseDirectory, "shot_noexif.jpg");
                        try
                        {
                            using var src = System.Drawing.Image.FromFile(demo);
                            using var bmp = new System.Drawing.Bitmap(src);
                            bmp.Save(bare, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                        catch
                        {
                            bare = demo;
                        }

                        window.DebugLoadImages(new[] { demo, bare });
                        window.DebugSelectIndex(fill ? 0 : 1);
                        if (fill) window.DebugFillDefaults();
                        else window.DebugApplyPresetExif();
                    }
                    else
                    {
                        window.DebugLoadImages(new[] { demo });
                        window.DebugSelectIndex(0);
                    }
                }

                await Task.Delay(900);

                try
                {
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "shot.log"),
                        $"Actual={window.ActualWidth}x{window.ActualHeight} Dpi={dpi.DpiScaleX}x{dpi.DpiScaleY} " +
                        $"WorkArea={SystemParameters.WorkArea.Width}x{SystemParameters.WorkArea.Height} " +
                        $"Primary={SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}");

                    SaveShot(window, "shot.png");

                    var dialog = Views.AppDialog.Create(window, "删除预设",
                        "确定要删除预设「Canon 经典」吗？这个操作不可撤销。", "删除", true, true);
                    dialog.Show();
                    await Task.Delay(400);
                    SaveShot(dialog, "shot_dialog.png");
                }
                catch
                {
                    // ignore
                }

                Shutdown();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void SaveShot(Window window, string fileName)
        {
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            var target = new RenderTargetBitmap(
                (int)(window.ActualWidth * dpi.DpiScaleX),
                (int)(window.ActualHeight * dpi.DpiScaleY),
                96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY,
                System.Windows.Media.PixelFormats.Pbgra32);
            target.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, fileName));
            encoder.Save(stream);
        }

        /// <summary>
        /// 无界面的自检：走完整的「队列 → 套预设 → 导出」链路，验证绘制与命名。
        /// </summary>
        private void RunSelfTest()
        {
            Dispatcher.InvokeAsync(async () =>
            {
                var log = new List<string>();
                try
                {
                    string baseDir = AppContext.BaseDirectory;
                    string demo = Path.Combine(baseDir, "UserData", "Input", "Canon", "demo.jpg");
                    string outDir = Path.Combine(baseDir, "selftest_out");
                    if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

                    var vm = new ViewModels.MainViewModel();
                    vm.AddFiles(new[] { demo });
                    log.Add($"queue = {vm.Queue.Count}, preset = {vm.SelectedPreset?.Name}");

                    var exif = vm.Queue[0].Exif;
                    log.Add($"exif  = {exif.Model} | {exif.LensModel} | {exif.FocalLength}mm {exif.ApertureValue} {exif.ShutterSpeedValue} ISO{exif.ISO} | {exif.DateTime}");
                    log.Add($"miss  = {string.Join(",", exif.AllFields.Where(f => !f.HasRaw).Select(f => f.Key))}");
                    log.Add($"state = {string.Join(",", exif.AllFields.Select(f => $"{f.Key}:{f.State}"))}");

                    vm.Settings.OutputDir = outDir;
                    vm.Settings.OpenAfterExport = false;

                    var (ok, errors) = await vm.ExportAllAsync();
                    log.Add($"export ok={ok} errors={errors.Count}");
                    foreach (var e in errors) log.Add("  err: " + e);

                    foreach (var f in Directory.GetFiles(outDir))
                        log.Add($"file  = {Path.GetFileName(f)} ({new FileInfo(f).Length} bytes)");

                    // 缺 EXIF 的图片：用预设兜底值
                    string plain = Path.Combine(baseDir, "selftest_plain.jpg");
                    using (var bmp = new System.Drawing.Bitmap(1200, 800))
                    {
                        using var g = System.Drawing.Graphics.FromImage(bmp);
                        g.Clear(System.Drawing.Color.SteelBlue);
                        bmp.Save(plain, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }

                    var vm2 = new ViewModels.MainViewModel();
                    vm2.AddFiles(new[] { plain });
                    var exif2 = vm2.Queue[0].Exif;
                    log.Add($"plain miss = {string.Join(",", exif2.AllFields.Where(f => !f.HasRaw).Select(f => f.Key))}");
                    log.Add($"plain state = {string.Join(",", exif2.AllFields.Select(f => $"{f.Key}:{f.State}"))}");
                    log.Add($"plain vals = {exif2.Model} | {exif2.LensModel} | {exif2.FocalLength}mm | {exif2.DateTime}");

                    vm2.Settings.OutputDir = outDir;
                    vm2.Settings.OpenAfterExport = false;
                    var (ok2, errors2) = await vm2.ExportAllAsync();
                    log.Add($"plain export ok={ok2} errors={errors2.Count}");
                    foreach (var f in Directory.GetFiles(outDir, "*plain*"))
                        log.Add($"file  = {Path.GetFileName(f)}");

                    // 预设默认 EXIF：空图套用 → 改默认值跟着变
                    var vm3 = new ViewModels.MainViewModel();
                    vm3.AddFiles(new[] { plain });
                    vm3.ApplyPresetExif();
                    var f3 = vm3.Queue[0].Exif;
                    log.Add($"preset exif = {f3.Model} | {f3.LensModel} | {f3.FocalLength}mm {f3.ApertureValue} {f3.ShutterSpeedValue} ISO{f3.ISO}");
                    log.Add($"preset state = {string.Join(",", f3.AllFields.Select(x => $"{x.Key}:{x.State}"))}");

                    if (vm3.SelectedPreset != null)
                    {
                        vm3.SelectedPreset.Defaults.ISO = "640";
                        log.Add($"after edit default ISO: value={f3.ISO} state={f3.AllFields.First(x => x.Key == "ISO").State}");
                    }

                    log.Add("result = OK");
                }
                catch (Exception ex)
                {
                    log.Add("result = FAIL");
                    log.Add(ex.ToString());
                }

                try
                {
                    File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "selftest.log"), log);
                }
                catch
                {
                    // ignore
                }

                Shutdown();
            });
        }
    }
}
