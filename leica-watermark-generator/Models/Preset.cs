using System.Collections.ObjectModel;

namespace leica_watermark_generator.Models
{
    /// <summary>
    /// 预设里的「默认 EXIF」。没有 EXIF 的图片可以一键套用这套值。
    /// 只是给用户填的模板，不会自动套到任何图片上。
    /// </summary>
    public class ExifDefaults : Observable
    {
        private string _make = "", _model = "", _lensModel = "", _focalLength = "";
        private string _aperture = "", _shutter = "", _iso = "", _dateTime = "";

        public string Make { get => _make; set => Set(ref _make, value); }
        public string Model { get => _model; set => Set(ref _model, value); }
        public string LensModel { get => _lensModel; set => Set(ref _lensModel, value); }
        public string FocalLength { get => _focalLength; set => Set(ref _focalLength, value); }
        public string ApertureValue { get => _aperture; set => Set(ref _aperture, value); }
        public string ShutterSpeedValue { get => _shutter; set => Set(ref _shutter, value); }
        public string ISO { get => _iso; set => Set(ref _iso, value); }
        public string DateTime { get => _dateTime; set => Set(ref _dateTime, value); }

        public string this[string key]
        {
            get => key switch
            {
                "Make" => Make,
                "Model" => Model,
                "LensModel" => LensModel,
                "FocalLength" => FocalLength,
                "ApertureValue" => ApertureValue,
                "ShutterSpeedValue" => ShutterSpeedValue,
                "ISO" => ISO,
                "DateTime" => DateTime,
                _ => ""
            };
            set
            {
                switch (key)
                {
                    case "Make": Make = value; break;
                    case "Model": Model = value; break;
                    case "LensModel": LensModel = value; break;
                    case "FocalLength": FocalLength = value; break;
                    case "ApertureValue": ApertureValue = value; break;
                    case "ShutterSpeedValue": ShutterSpeedValue = value; break;
                    case "ISO": ISO = value; break;
                    case "DateTime": DateTime = value; break;
                }
            }
        }

        public ExifDefaults Clone() => new()
        {
            Make = Make,
            Model = Model,
            LensModel = LensModel,
            FocalLength = FocalLength,
            ApertureValue = ApertureValue,
            ShutterSpeedValue = ShutterSpeedValue,
            ISO = ISO,
            DateTime = DateTime
        };
    }

    /// <summary>
    /// 预设 = 一套水印样式（文字、图标、布局）+ 一份默认 EXIF。
    /// 队列里所有图片默认套用当前选中的预设。
    /// </summary>
    public class Preset : Observable
    {
        private string _name = "新建预设";

        public string Name { get => _name; set => Set(ref _name, value); }

        public WatermarkStyle Style { get; set; } = WatermarkStyle.CreateDefault();

        /// <summary>没有 EXIF 的图片可以一键套用的默认参数</summary>
        public ExifDefaults Defaults { get; set; } = new();

        /// <summary>默认 EXIF 是否已经初始化过（老版本存的预设要补一次）</summary>
        public bool DefaultsInitialized { get; set; }

        public Preset Clone(string? newName = null)
        {
            return new Preset
            {
                Name = newName ?? Name,
                Style = Style.Clone(),
                Defaults = Defaults.Clone()
            };
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// 程序设置（与预设一起保存在 presets.json）
    /// </summary>
    public class AppSettings
    {
        public string OutputDir { get; set; } = "";
        public string NamingPattern { get; set; } = "{filename}_leica_watermark";
        public long Quality { get; set; } = 99;
        public bool Overwrite { get; set; } = false;
        public bool OpenAfterExport { get; set; } = true;
        public string FontFamily { get; set; } = "PingFang SC Medium";
    }

    /// <summary>presets.json 的根节点</summary>
    public class PresetFile
    {
        public ObservableCollection<Preset> Presets { get; set; } = new();
        public string CurrentPreset { get; set; } = "";
        public AppSettings Settings { get; set; } = new();
    }
}
