using System.IO;
using System.Text;
using leica_watermark_generator.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace leica_watermark_generator.Core
{
    /// <summary>
    /// 预设的读写。默认存在程序目录下的 presets.json，首次运行会自动生成，
    /// 并尝试把老版本的 config.json 里的预设迁移过来。
    /// </summary>
    public static class PresetStore
    {
        public static string FilePath => Path.Combine(AppContext.BaseDirectory, "presets.json");

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static PresetFile Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var json = File.ReadAllText(FilePath, Encoding.UTF8);
                    var file = JsonConvert.DeserializeObject<PresetFile>(json, JsonSettings);
                    if (file != null && file.Presets.Count > 0)
                    {
                        Normalize(file);
                        return file;
                    }
                }
                catch
                {
                    // 配置文件损坏时退回默认预设，不打断启动
                }
            }

            var defaults = CreateDefaults();
            TryImportLegacyConfig(defaults);
            return defaults;
        }

        public static void Save(PresetFile file)
        {
            var json = JsonConvert.SerializeObject(file, JsonSettings);
            File.WriteAllText(FilePath, json, new UTF8Encoding(false));
        }

        private static void Normalize(PresetFile file)
        {
            file.Presets ??= new System.Collections.ObjectModel.ObservableCollection<Preset>();
            file.Settings ??= new AppSettings();

            foreach (var preset in file.Presets)
            {
                preset.Style ??= WatermarkStyle.CreateDefault();
                preset.Defaults ??= new ExifDefaults();

                // 老版本的 presets.json 里没有默认 EXIF，先按预设名去老 config.json 找，
                // 内置预设则补上自带的那套
                if (!preset.DefaultsInitialized)
                {
                    SeedDefaultsFromLegacy(preset);
                    if (string.IsNullOrEmpty(preset.Defaults.Model)) ApplyBuiltinDefaults(preset);
                    preset.DefaultsInitialized = true;
                }
                preset.Style.Separator ??= new SeparatorConfig();
                preset.Style.Logo ??= new LogoConfig();
                preset.Style.Lines ??= new System.Collections.ObjectModel.ObservableCollection<TextLine>();

                if (preset.Style.Lines.Count == 0)
                {
                    foreach (var line in WatermarkStyle.CreateDefault().Lines)
                        preset.Style.Lines.Add(line);
                }

                if (preset.Style.BaseWidth < 100) preset.Style.BaseWidth = 4032;
                if (preset.Style.StripeHeight < 20) preset.Style.StripeHeight = 354;
                if (preset.Style.FocalLengthMultiplier <= 0) preset.Style.FocalLengthMultiplier = 1.0;
            }

            if (string.IsNullOrEmpty(file.CurrentPreset) ||
                !file.Presets.Any(p => p.Name == file.CurrentPreset))
            {
                file.CurrentPreset = file.Presets[0].Name;
            }
        }

        public static PresetFile CreateDefaults()
        {
            var file = new PresetFile();
            file.Settings.NamingPattern = "{filename}_leica_watermark";
            file.Settings.Quality = 99;

            var canon = new Preset { Name = "Canon 经典" };
            canon.Style.Logo.File = "img/Canon.png";
            canon.DefaultsInitialized = true;
            canon.Defaults.Make = "Canon";
            canon.Defaults.Model = "EOS R5";
            canon.Defaults.LensModel = "RF 24-70mm F2.8 L IS USM";
            canon.Defaults.FocalLength = "50";
            canon.Defaults.ApertureValue = "f/2.8";
            canon.Defaults.ShutterSpeedValue = "1/500″";
            canon.Defaults.ISO = "200";
            file.Presets.Add(canon);

            var sony = new Preset { Name = "SONY 经典" };
            sony.Style.Logo.File = "img/SONY.png";
            sony.DefaultsInitialized = true;
            sony.Defaults.Make = "SONY";
            sony.Defaults.Model = "ILCE-7M4";
            sony.Defaults.LensModel = "FE 24-70mm F2.8 GM II";
            sony.Defaults.FocalLength = "50";
            sony.Defaults.ApertureValue = "f/2.8";
            sony.Defaults.ShutterSpeedValue = "1/250″";
            sony.Defaults.ISO = "100";
            file.Presets.Add(sony);

            var dark = new Preset { Name = "暗调黑底" };
            dark.DefaultsInitialized = true;
            dark.Defaults.Make = "Nikon";
            dark.Defaults.Model = "Z 8";
            dark.Defaults.LensModel = "NIKKOR Z 24-70mm f/2.8 S";
            dark.Defaults.FocalLength = "35";
            dark.Defaults.ApertureValue = "f/4.0";
            dark.Defaults.ShutterSpeedValue = "1/250″";
            dark.Defaults.ISO = "400";
            dark.Style.Background = "#0B0B0C";
            dark.Style.Logo.File = "img/Nikon.png";
            dark.Style.Lines[0].Color = "#FFFFFF";
            dark.Style.Lines[0].StrokeWidth = 0;
            dark.Style.Lines[1].Color = "#9A9A98";
            dark.Style.Lines[2].Color = "#FFFFFF";
            dark.Style.Lines[2].StrokeWidth = 0;
            dark.Style.Lines[3].Color = "#9A9A98";
            dark.Style.Separator.Color = "#5A5A58";
            file.Presets.Add(dark);

            file.CurrentPreset = canon.Name;
            return file;
        }

        /// <summary>老 config.json 里的预设项 → 默认 EXIF</summary>
        private static void CopyLegacyDefaults(JToken item, Preset preset)
        {
            preset.Defaults.Make = item["Make"]?.Value<string>() ?? "";
            preset.Defaults.Model = item["Model"]?.Value<string>() ?? "";
            preset.Defaults.LensModel = item["LensModel"]?.Value<string>() ?? "";
            preset.Defaults.FocalLength = item["FocalLength"]?.Value<string>() ?? "";
            preset.Defaults.ApertureValue = item["ApertureValue"]?.Value<string>() ?? "";
            preset.Defaults.ShutterSpeedValue = item["ShutterSpeedValue"]?.Value<string>() ?? "";
            preset.Defaults.ISO = item["ISOSpeed"]?.Value<string>() ?? "";
        }

        /// <summary>内置预设自带的默认 EXIF（老文件升级时补上）</summary>
        private static void ApplyBuiltinDefaults(Preset preset)
        {
            var (make, model, lens, focal, aperture, shutter, iso) = preset.Name switch
            {
                "Canon 经典" => ("Canon", "EOS R5", "RF 24-70mm F2.8 L IS USM", "50", "f/2.8", "1/500″", "200"),
                "SONY 经典" => ("SONY", "ILCE-7M4", "FE 24-70mm F2.8 GM II", "50", "f/2.8", "1/250″", "100"),
                "暗调黑底" => ("Nikon", "Z 8", "NIKKOR Z 24-70mm f/2.8 S", "35", "f/4.0", "1/250″", "400"),
                _ => ("", "", "", "", "", "", "")
            };

            if (string.IsNullOrEmpty(model)) return;

            preset.Defaults.Make = make;
            preset.Defaults.Model = model;
            preset.Defaults.LensModel = lens;
            preset.Defaults.FocalLength = focal;
            preset.Defaults.ApertureValue = aperture;
            preset.Defaults.ShutterSpeedValue = shutter;
            preset.Defaults.ISO = iso;
        }

        private static JObject? _legacyRoot;

        /// <summary>按预设名去老配置里找一份默认 EXIF（只对没有初始化的预设生效）</summary>
        private static void SeedDefaultsFromLegacy(Preset preset)
        {
            try
            {
                if (_legacyRoot == null)
                {
                    var legacy = Path.Combine(AppContext.BaseDirectory, "config.json");
                    _legacyRoot = File.Exists(legacy)
                        ? JObject.Parse(File.ReadAllText(legacy, Encoding.UTF8))
                        : new JObject();
                }

                if (_legacyRoot["NoExifImageConfig"]?["PresetSettings"]?["Presets"] is not JArray list) return;

                foreach (var item in list)
                {
                    if (item["PresetName"]?.Value<string>() != preset.Name) continue;
                    CopyLegacyDefaults(item, preset);
                    return;
                }
            }
            catch
            {
                // 迁移失败就当没有
            }
        }

        /// <summary>按厂牌名去 img 目录里猜一个 Logo（SONY → img/SONY.png）</summary>
        private static string GuessLogo(string make)
        {
            if (string.IsNullOrWhiteSpace(make)) return "";

            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "img");
                if (!Directory.Exists(dir)) return "";

                foreach (var file in Directory.GetFiles(dir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.Equals(make, StringComparison.OrdinalIgnoreCase) ||
                        make.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(make, StringComparison.OrdinalIgnoreCase))
                    {
                        return "img/" + Path.GetFileName(file);
                    }
                }
            }
            catch
            {
                // ignore
            }

            return "";
        }

        /// <summary>把老 config.json 里的预设/质量/等效焦距系数迁移过来</summary>
        private static void TryImportLegacyConfig(PresetFile file)
        {
            try
            {
                var legacy = Path.Combine(AppContext.BaseDirectory, "config.json");
                if (!File.Exists(legacy)) return;

                var root = JObject.Parse(File.ReadAllText(legacy, Encoding.UTF8));

                if (root["SaveJPG_Quality"] != null)
                    file.Settings.Quality = root["SaveJPG_Quality"]!.Value<long>();

                double coefficient = 1.0;
                var current = root["EquivalentFocalLength"]?["Current"]?.Value<string>();
                if (root["EquivalentFocalLength"]?["DefineCoefficient"] is JArray coefficients)
                {
                    foreach (var item in coefficients)
                    {
                        if (item["CoefficientName"]?.Value<string>() == current)
                        {
                            coefficient = item["Coefficient"]?.Value<double>() ?? 1.0;
                            break;
                        }
                    }
                }

                if (root["NoExifImageConfig"]?["PresetSettings"]?["Presets"] is not JArray presets) return;

                foreach (var item in presets)
                {
                    var name = item["PresetName"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (file.Presets.Any(p => p.Name == name)) continue;

                    var preset = new Preset { Name = name };
                    preset.Style = WatermarkStyle.CreateDefault();
                    preset.Style.FocalLengthMultiplier = coefficient;
                    // 老配置里每个预设都带一套「无 EXIF 时用的假参数」，正好对应现在的「默认 EXIF」
                    CopyLegacyDefaults(item, preset);
                    preset.DefaultsInitialized = true;
                    preset.Style.Logo.File = GuessLogo(preset.Defaults.Make);
                    file.Presets.Add(preset);
                }

                var currentPreset = root["NoExifImageConfig"]?["PresetSettings"]?["Current"]?.Value<string>();
                if (!string.IsNullOrEmpty(currentPreset) &&
                    file.Presets.Any(p => p.Name == currentPreset))
                {
                    file.CurrentPreset = currentPreset;
                }
            }
            catch
            {
                // 迁移失败不影响默认预设
            }
        }
    }
}
