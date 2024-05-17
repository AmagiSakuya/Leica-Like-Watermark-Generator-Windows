using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using leica_watermark_generator.Scripts.DataModel;

namespace leica_watermark_generator.Scripts.Core
{
    internal class Draw
    {
        static PrivateFontCollection m_privateFonts = new PrivateFontCollection();
        static PrivateFontCollection privateFonts
        {
            get
            {
                if (m_privateFonts.Families.Length == 0)
                    m_privateFonts = FileManager.LoadEmbeddedFonts(new string[] { "PingFangSC-Medium" });
                return m_privateFonts;
            }
        }

        public static void DrawImg(ExifInfo exif_info, string jpgPath, string outputDir)
        {
            //string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts\\PingFangSC-Medium.ttf"); // 字体文件路径，替换为你的字体文件路径
            //PrivateFontCollection privateFonts = new PrivateFontCollection();
            //privateFonts.AddFontFile(fontPath);

            // 绘制watermark
            Bitmap waterMarkImage = new Bitmap(4032, 354);
            using (Graphics g = Graphics.FromImage(waterMarkImage))
            {
                // 绘制白色背景
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, 0, 0, waterMarkImage.Width, waterMarkImage.Height);
                }

                //绘制机型
                DrawText(g, string.Format("{0} {1}", exif_info.Make, exif_info.Model), new PointF(205, 82), privateFonts.Families[0], 58, 3, Color.Black, Color.Black);
                //镜头
                DrawText(g, exif_info.LensModel, new PointF(210, 200), privateFonts.Families[0], 38, 0, ColorTranslator.FromHtml("#7E7E7C"), Color.Black);
                //参数
                DrawText(g, string.Format("{0}mm {1} {2} ISO{3}", exif_info.FocalLength, exif_info.ApertureValue, exif_info.ShutterSpeedValue, exif_info.ISO), new PointF(3080, 90), privateFonts.Families[0], 45, 1, Color.Black, Color.Black);
                //拍摄时间
                DrawText(g, exif_info.DateTime, new PointF(3080, 200), privateFonts.Families[0], 38, 0, ColorTranslator.FromHtml("#7E7E7C"), Color.Black);
                //绘制竖线
                DrawLine(g, new PointF(3040, 110), new PointF(3040, 260), 4, ColorTranslator.FromHtml("#7E7E7C"));
                //绘制logo
                DrawLogo(g, new PointF(2525, 60), 470f, exif_info.MakeImagePath);
            }

            // 读取原图
            Bitmap originalImage = new Bitmap(jpgPath);
            // 计算缩放比例
            float scale = (float)originalImage.Width / waterMarkImage.Width;
            // 缩放水印
            Bitmap scaledWaterMarkImage = new Bitmap(waterMarkImage, new Size((int)(waterMarkImage.Width * scale), (int)(waterMarkImage.Height * scale)));
            // 创建输出
            Bitmap newImage = new Bitmap(originalImage.Width, originalImage.Height + scaledWaterMarkImage.Height);
            using (Graphics g = Graphics.FromImage(newImage))
            {
                // 在新图片上绘制原始图片
                g.DrawImage(originalImage, new Rectangle(0, 0, originalImage.Width, originalImage.Height));
                // 在新图片底部绘制缩放后的待拼接图片
                g.DrawImage(scaledWaterMarkImage, new Rectangle(0, originalImage.Height, scaledWaterMarkImage.Width, scaledWaterMarkImage.Height));
            }
            string outputFilePath = Path.Combine(outputDir, string.Format("{0}_leica_watermark.jpg", exif_info.FileName));
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, FileManager.configData.SaveJPG_Quality); // 设置质量为99%
            newImage.Save(outputFilePath, GetEncoder(ImageFormat.Jpeg), encoderParams);
            Console.WriteLine($"The image has been saved to: {outputFilePath}");
        }

        #region 绘制
        /// <summary>
        /// 绘制文字
        /// </summary>
        static void DrawText(Graphics g, string text, PointF pos, FontFamily fontFamily, int fontSize, int outlineWidth, Color textColor, Color outLineColor)
        {
            // 设置文字渲染的质量为抗锯齿
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            //绘制文字
            using (GraphicsPath path = new GraphicsPath())
            using (Pen pen = new Pen(outLineColor, outlineWidth))
            using (SolidBrush brush = new SolidBrush(textColor))
            using (Font font = new Font(fontFamily, fontSize))
            {
                if (outlineWidth > 0)
                {
                    //绘制描边
                    path.AddString(text, font.FontFamily, (int)font.Style, g.DpiY * fontSize / 72, pos, StringFormat.GenericDefault);
                    pen.LineJoin = LineJoin.Round; // 设置线条连接方式为圆角
                    g.DrawPath(pen, path);
                }

                //绘制主文字
                g.DrawString(text, font, brush, pos);
            }
        }

        /// <summary>
        /// 绘制线段
        /// </summary>
        static void DrawLine(Graphics g, PointF start, PointF end, int lineWidth, Color color)
        {
            using (Pen pen = new Pen(color, lineWidth))
            {
                g.DrawLine(pen, start, end);
            }
        }
        #endregion

        static void DrawLogo(Graphics g, PointF pos, float newWidth, string modelLogoRelativePath)
        {
            Bitmap originalImage = new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelLogoRelativePath));
            float scaleFactor = newWidth / originalImage.Width;
            int newHeight = (int)(originalImage.Height * scaleFactor);
            g.DrawImage(originalImage, pos.X, pos.Y, newWidth, newHeight);
        }

        #region Resources
        // 获取指定图像格式的编码器
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        #endregion
    }
}