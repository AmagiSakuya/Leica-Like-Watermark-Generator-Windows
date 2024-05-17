using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace leica_watermark_generator.DataModel
{

    [System.Serializable]
    public class ConfigJsonData
    {
        public MakerConfigStruct[] MakerConfig;
        public string[] IgnoreLensModel;
        public NoExifImageConfig NoExifImageConfig;
        public EquivalentFocalLengthStruct EquivalentFocalLength;
        public Int64 SaveJPG_Quality;
    }

    [System.Serializable]
    public class MakerConfigStruct
    {
        public string MakerName;
        public string MakerImage;
    }

    [System.Serializable]
    public class NoExifImageConfig
    {
        public bool UsePresetInsteadRealtimeUserSelection;
        public PresetSturct Preset;
        public string[] ModelOptions;
        public string[] LensModelOptions;
        public string[] FocalLengthOptions;
        public string[] ShutterSpeedValueOptions;
        public string[] ApertureValueOptions;
        public string[] ISOSpeedRatings;
    }

    [System.Serializable]
    public class PresetSturct
    {
        public string Make;
        public string Model;
        public string LensModel;
        public float FocalLength;
        public string ISOSpeed;
        public string ShutterSpeedValue;
        public string ApertureValue;
        public string DateTime;
    }

    [System.Serializable]
    public class EquivalentFocalLengthStruct
    {
        public string Current;
        public DefineCoefficientStruct[] DefineCoefficient;
    }

    [System.Serializable]
    public class DefineCoefficientStruct
    {
        public string CoefficientName;
        public float Coefficient;
    }
}
