using System.Collections.ObjectModel;

namespace leica_watermark_generator.Models
{
    /// <summary>
    /// 一行水印文字。坐标与字号都基于 <see cref="WatermarkStyle.BaseWidth"/> 定义的坐标系（默认 4032 宽）。
    /// </summary>
    public class TextLine : Observable
    {
        private string _name = "文字";
        private string _template = "{Model}";
        private bool _enabled = true;
        private string _fontFamily = "PingFang SC Medium";
        private double _size = 58;
        private string _color = "#000000";
        private double _strokeWidth = 0;
        private string _strokeColor = "#000000";
        private double _x = 200;
        private double _y = 100;
        private double _opacity = 1;

        /// <summary>在列表里显示的名字，不影响渲染</summary>
        public string Name { get => _name; set => Set(ref _name, value); }

        /// <summary>内容模板，支持 {Model} {LensModel} {FocalLength} 等占位符</summary>
        public string Template { get => _template; set => Set(ref _template, value); }

        public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

        public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }

        /// <summary>字号，单位像素（基准坐标系下）</summary>
        public double Size { get => _size; set => Set(ref _size, value); }

        public string Color { get => _color; set => Set(ref _color, value); }

        public double StrokeWidth { get => _strokeWidth; set => Set(ref _strokeWidth, value); }

        public string StrokeColor { get => _strokeColor; set => Set(ref _strokeColor, value); }

        public double X { get => _x; set => Set(ref _x, value); }

        public double Y { get => _y; set => Set(ref _y, value); }

        public double Opacity { get => _opacity; set => Set(ref _opacity, value); }

        public TextLine Clone()
        {
            return new TextLine
            {
                Name = Name,
                Template = Template,
                Enabled = Enabled,
                FontFamily = FontFamily,
                Size = Size,
                Color = Color,
                StrokeWidth = StrokeWidth,
                StrokeColor = StrokeColor,
                X = X,
                Y = Y,
                Opacity = Opacity
            };
        }
    }

    /// <summary>水印中间的竖直分隔线</summary>
    public class SeparatorConfig : Observable
    {
        private bool _enabled = true;
        private double _x = 3040;
        private double _y1 = 110;
        private double _y2 = 260;
        private double _width = 4;
        private string _color = "#7E7E7C";

        public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
        public double X { get => _x; set => Set(ref _x, value); }
        public double Y1 { get => _y1; set => Set(ref _y1, value); }
        public double Y2 { get => _y2; set => Set(ref _y2, value); }
        public double Width { get => _width; set => Set(ref _width, value); }
        public string Color { get => _color; set => Set(ref _color, value); }

        public SeparatorConfig Clone() => new()
        {
            Enabled = Enabled, X = X, Y1 = Y1, Y2 = Y2, Width = Width, Color = Color
        };
    }

    /// <summary>品牌 Logo（PNG，带 alpha）</summary>
    public class LogoConfig : Observable
    {
        private bool _enabled = true;
        private string _file = "";
        private double _x = 2525;
        private double _y = 60;
        private double _width = 470;
        private double _opacity = 1;

        public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

        /// <summary>PNG 路径，可以是绝对路径，也可以是相对程序目录的路径</summary>
        public string File { get => _file; set => Set(ref _file, value); }

        public double X { get => _x; set => Set(ref _x, value); }
        public double Y { get => _y; set => Set(ref _y, value); }

        /// <summary>Logo 宽度（基准坐标系像素），高度按原图比例</summary>
        public double Width { get => _width; set => Set(ref _width, value); }

        public double Opacity { get => _opacity; set => Set(ref _opacity, value); }

        public LogoConfig Clone() => new()
        {
            Enabled = Enabled, File = File, X = X, Y = Y, Width = Width, Opacity = Opacity
        };
    }

    /// <summary>
    /// 一套水印样式：画布尺寸 + 若干文字行 + 分隔线 + Logo。
    /// 默认值等于老版本 Draw.cs 里写死的布局，所以新建预设开箱即是原来的效果。
    /// </summary>
    public class WatermarkStyle : Observable
    {
        private int _baseWidth = 4032;
        private int _stripeHeight = 354;
        private string _background = "#FFFFFF";
        private double _focalLengthMultiplier = 1.0;

        public int BaseWidth { get => _baseWidth; set => Set(ref _baseWidth, value); }

        public int StripeHeight { get => _stripeHeight; set => Set(ref _stripeHeight, value); }

        /// <summary>水印条背景色，支持 #RRGGBB / #AARRGGBB</summary>
        public string Background { get => _background; set => Set(ref _background, value); }

        /// <summary>等效焦距系数，焦段显示会乘上这个值（APS-C 填 1.5 之类）</summary>
        public double FocalLengthMultiplier
        {
            get => _focalLengthMultiplier;
            set => Set(ref _focalLengthMultiplier, value);
        }

        public ObservableCollection<TextLine> Lines { get; set; } = new();

        public SeparatorConfig Separator { get; set; } = new();

        public LogoConfig Logo { get; set; } = new();

        public WatermarkStyle Clone()
        {
            var s = new WatermarkStyle
            {
                BaseWidth = BaseWidth,
                StripeHeight = StripeHeight,
                Background = Background,
                FocalLengthMultiplier = FocalLengthMultiplier,
                Separator = Separator.Clone(),
                Logo = Logo.Clone()
            };
            foreach (var line in Lines) s.Lines.Add(line.Clone());
            return s;
        }

        /// <summary>出厂默认样式 = 老版本硬编码的布局</summary>
        public static WatermarkStyle CreateDefault()
        {
            var style = new WatermarkStyle();
            style.Lines.Add(new TextLine
            {
                Name = "机型", Template = "{Model}", X = 205, Y = 82,
                Size = 77, Color = "#000000", StrokeWidth = 4, StrokeColor = "#000000"
            });
            style.Lines.Add(new TextLine
            {
                Name = "镜头", Template = "{LensModel}", X = 210, Y = 200,
                Size = 51, Color = "#7E7E7C", StrokeWidth = 0
            });
            style.Lines.Add(new TextLine
            {
                Name = "参数", Template = "{FocalLength}mm {Aperture} {Shutter} ISO{ISO}",
                X = 3080, Y = 92, Size = 60, Color = "#000000", StrokeWidth = 1
            });
            style.Lines.Add(new TextLine
            {
                Name = "拍摄时间", Template = "{DateTime}", X = 3080, Y = 200,
                Size = 51, Color = "#7E7E7C", StrokeWidth = 0
            });
            return style;
        }
    }
}
