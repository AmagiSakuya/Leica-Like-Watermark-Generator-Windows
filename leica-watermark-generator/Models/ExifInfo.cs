namespace leica_watermark_generator.Models
{
    /// <summary>字段里当前值的来源，用于界面上的颜色提示</summary>
    public enum FieldState
    {
        /// <summary>和文件里的 EXIF 一致（或都为空）</summary>
        Exif,
        /// <summary>取自预设里的「默认 EXIF」</summary>
        Preset,
        /// <summary>被手动改过，既不等于原值也不等于预设值</summary>
        Manual
    }

    /// <summary>
    /// 一个可编辑的拍摄参数。记住「文件里读到的原值」和「预设里的默认值」，
    /// 界面改过的值只在本次导出用，不写回原图。
    /// </summary>
    public class ExifField : Observable
    {
        private string _value = "";
        private string _presetValue = "";

        public ExifField(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }

        public string Label { get; }

        /// <summary>文件里读到的原始值，读不到就是空</summary>
        public string Raw { get; set; } = "";

        public bool HasRaw => !string.IsNullOrEmpty(Raw);

        /// <summary>当前使用的值（导出用的就是它）</summary>
        public string Value
        {
            get => _value;
            set
            {
                if (Set(ref _value, value)) Raise(nameof(State));
            }
        }

        /// <summary>预设里的默认 EXIF 值，只用来标注来源，不会自动套到图片上</summary>
        public string PresetValue
        {
            get => _presetValue;
            set
            {
                if (!Set(ref _presetValue, value)) return;
                Raise(nameof(State));
            }
        }

        public FieldState State
        {
            get
            {
                if (Value == Raw) return FieldState.Exif;
                if (!string.IsNullOrEmpty(PresetValue) && Value == PresetValue) return FieldState.Preset;
                return FieldState.Manual;
            }
        }

        /// <summary>恢复到文件里的原值</summary>
        public void ResetToRaw() => Value = Raw;

        /// <summary>用预设的默认值（可能为空）</summary>
        public void UsePreset() => Value = PresetValue;
    }

    /// <summary>
    /// 一张图片的拍摄参数。字段固定七个，按「宽 / 两列 / 宽」的布局排。
    /// </summary>
    public class ExifInfo : Observable
    {
        private readonly ExifField _make = new("Make", "品牌");
        private readonly ExifField _model = new("Model", "机型");
        private readonly ExifField _lensModel = new("LensModel", "镜头");
        private readonly ExifField _focalLength = new("FocalLength", "焦段");
        private readonly ExifField _aperture = new("ApertureValue", "光圈");
        private readonly ExifField _shutter = new("ShutterSpeedValue", "快门");
        private readonly ExifField _iso = new("ISO", "ISO");
        private readonly ExifField _dateTime = new("DateTime", "时间");

        public ExifField MakeField => _make;
        public ExifField ModelField => _model;
        public ExifField LensField => _lensModel;
        public ExifField FocalField => _focalLength;
        public ExifField ApertureField => _aperture;
        public ExifField ShutterField => _shutter;
        public ExifField IsoField => _iso;
        public ExifField DateTimeField => _dateTime;

        /// <summary>全部字段（含界面上不显示的品牌）</summary>
        public IEnumerable<ExifField> AllFields => new[]
        {
            _make, _model, _lensModel, _focalLength, _aperture, _shutter, _iso, _dateTime
        };

        /// <summary>文件名（不含扩展名），供 {FileName} 占位符使用</summary>
        public string FileName { get; set; } = "";

        public string this[string key]
        {
            get => AllFields.FirstOrDefault(f => f.Key == key)?.Value ?? "";
            set
            {
                var field = AllFields.FirstOrDefault(f => f.Key == key);
                if (field != null) field.Value = value;
            }
        }

        public string Make => _make.Value;
        public string Model => _model.Value;
        public string LensModel => _lensModel.Value;
        public string FocalLength => _focalLength.Value;
        public string ApertureValue => _aperture.Value;
        public string ShutterSpeedValue => _shutter.Value;
        public string ISO => _iso.Value;
        public string DateTime => _dateTime.Value;

        /// <summary>是否有字段在 EXIF 里读不到</summary>
        public bool HasMissing => AllFields.Any(f => !f.HasRaw);

        /// <summary>文件里是否读到了任何 EXIF</summary>
        public bool HasAnyRaw => AllFields.Any(f => f.HasRaw);

        public void SetRaw(string key, string value)
        {
            var field = AllFields.FirstOrDefault(f => f.Key == key);
            if (field == null) return;
            field.Raw = value ?? "";
            field.Value = field.Raw;
        }

        /// <summary>把预设里的默认 EXIF 挂到字段上（只标注来源，不改当前值）</summary>
        public void PushPreset(ExifDefaults defaults)
        {
            foreach (var field in AllFields) field.PresetValue = defaults[field.Key];
        }

        /// <summary>把预设的默认值填进所有字段（导出时用的值）</summary>
        public void ApplyPreset()
        {
            foreach (var field in AllFields) field.UsePreset();
        }
    }
}
