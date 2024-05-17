using System.Drawing.Text;
using System.Resources;
using Newtonsoft.Json;
using leica_watermark_generator.DataModel;
using leica_watermark_generator.Scripts.DataModel;
using ExifLibrary;

namespace leica_watermark_generator.Scripts.Core
{
    internal class FileManager
    {
        public static ConfigJsonData configData = LoadConfig();

        //public static 
        #region Load IO File 
        public static bool EnSureDir(string dirPath, string tipPath)
        {
            if (!Directory.Exists(dirPath))
            {
                try
                {
                    // 创建 Sony 文件夹
                    Directory.CreateDirectory(dirPath);
                    Console.WriteLine(String.Format("[{0}] folder created", tipPath));
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(String.Format("Unable to create [{0}] folder: {1}", tipPath, ex.Message));
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        public static string[] ReadDirJPGs(string dirPath, string tipsUseDir)
        {
            // 读取 Sony 文件夹下的 JPG 图片
            string[] jpgFiles = Directory.GetFiles(dirPath, "*.jpg");
            Console.WriteLine($"Found {jpgFiles.Length} JPG images in [{tipsUseDir}]");
            return jpgFiles;
        }
        #endregion

        #region Load Embedded File 
        /// <summary>
        ///  加载嵌入的字体资源并返回 PrivateFontCollection 对象
        /// </summary>
        public static PrivateFontCollection LoadEmbeddedFonts(string[] fontNames)
        {
            ResourceManager resManager = new ResourceManager("leica_watermark_generator.Properties.Resources", typeof(Program).Assembly);

            PrivateFontCollection privateFonts = new PrivateFontCollection();

            foreach (var fontName in fontNames)
            {
                // 将字体流加载到字节数组中
                byte[] fontData = (byte[])resManager.GetObject(fontName);
                // 将字体字节数组添加到私有字体集合中
                IntPtr fontPtr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(fontData.Length);
                System.Runtime.InteropServices.Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
                privateFonts.AddMemoryFont(fontPtr, fontData.Length);
                // 释放资源
                System.Runtime.InteropServices.Marshal.FreeCoTaskMem(fontPtr);
            }

            return privateFonts;
        }

        #endregion

        #region Load Config File 
        public static ConfigJsonData LoadConfig()
        {
            string jsonContent = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
            return JsonConvert.DeserializeObject<ConfigJsonData>(jsonContent);
        }
        #endregion

        #region Load Exif 
        /// <summary>
        /// Load Exif
        /// </summary>
        public static ExifInfo LoadExif(string jpgPath)
        {
            ExifInfo res = new ExifInfo();
            var file = ImageFile.FromFile(jpgPath);
            //res.Make = TryGetExifTag<ExifAscii>(file, ExifTag.Make);
            res.Model = TryGetExifTag<ExifAscii>(file, ExifTag.Model);
            res.LensModel = GetLensModel(file);
            res.FocalLength = GetFocalLength(file);
            res.ShutterSpeedValue = ConvertApexShutterSpeed(file.Properties.Get<ExifSRational>(ExifTag.ShutterSpeedValue));
            res.ApertureValue = ConvertApexAperture(file.Properties.Get<ExifURational>(ExifTag.ApertureValue));
            res.ISO = TryGetExifTag<ExifUShort>(file, ExifTag.ISOSpeedRatings);
            res.DateTime = TryGetExifTag<ExifDateTime>(file, ExifTag.DateTime);
            return res;
        }

        static string TryGetExifTag<ExifDataType>(ImageFile file, ExifTag tag) where ExifDataType : ExifProperty
        {
            if (ExifTagHasValue<ExifDataType>(file, tag))
            {
                return file.Properties.Get<ExifDataType>(tag).Value.ToString();
            }
            else
            {
                return UserInputManager.UserSureExif(tag);
            }
        }

        static bool ExifTagHasValue<ExifDataType>(ImageFile file, ExifTag tag) where ExifDataType : ExifProperty
        {
            return file.Properties.Get<ExifDataType>(tag) != null && file.Properties.Get<ExifDataType>(tag).Value != null;
        }

        //APEX快门转普通显示
        static string ConvertApexShutterSpeed(ExifSRational shutterSpeedValue)
        {
            if (shutterSpeedValue == null)
            {
                return UserInputManager.UserSureExif(ExifTag.ShutterSpeedValue) + "″";
            }
            double apexDouble = (double)shutterSpeedValue.Value.Numerator / shutterSpeedValue.Value.Denominator;
            double shutterSpeed = Math.Pow(2, apexDouble);
            //int denominator = (int)Math.Round(1 / shutterSpeed);

            if (shutterSpeed < 1)
            {

                return shutterSpeed + "″";
            }
            else
            {
                return "1/" + Math.Round(shutterSpeed) + "″";
            }
        }

        //APEX光圈转普通显示
        static string ConvertApexAperture(ExifURational apertureValue)
        {
            if (apertureValue == null)
            {
                return UserInputManager.UserSureExif(ExifTag.ApertureValue);
            }
            double apertureDouble = (double)apertureValue.Value.Numerator / apertureValue.Value.Denominator;
            double apertureSize = Math.Pow(Math.Sqrt(2), apertureDouble);
            return "f/" + apertureSize.ToString("0.0");
        }

        static float ConvertApexFocalLength(ExifURational FocalLength)
        {
            return (FocalLength.Value.Numerator / FocalLength.Value.Denominator);
        }

        static string GetFocalLength(ImageFile file)
        {
            float focalLength;
            if (ExifTagHasValue<ExifURational>(file, ExifTag.FocalLength))
            {
                focalLength = ConvertApexFocalLength(file.Properties.Get<ExifURational>(ExifTag.FocalLength));
            }
            else
            {
                focalLength = float.Parse(UserInputManager.UserSureExif(ExifTag.FocalLength));
            }
            //乘以配置的等效转换系数 向上取整
            focalLength *= GetConfigEquivalentCoefficient();
            focalLength = (int)Math.Ceiling(focalLength);
            return focalLength.ToString();
        }

        static float GetConfigEquivalentCoefficient()
        {
            float coefficient = 1.0f;
            for (int i = 0; i < configData.EquivalentFocalLength.DefineCoefficient.Length; i++)
            {
                if (configData.EquivalentFocalLength.DefineCoefficient[i].CoefficientName == configData.EquivalentFocalLength.Current)
                {
                    coefficient = configData.EquivalentFocalLength.DefineCoefficient[i].Coefficient;
                }
            }
            return coefficient;
        }

        static string GetLensModel(ImageFile file)
        {
            string lensModel = TryGetExifTag<ExifAscii>(file, ExifTag.LensModel);
            if (configData.IgnoreLensModel.Contains(lensModel))
            {
                lensModel = UserInputManager.UserSureExif(ExifTag.LensModel);
            }
            return lensModel;
        }

        public static void LogImageInfo(ExifInfo res)
        {
            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine(String.Format("{0}:{1}", "FileName", res.FileName));
            Console.WriteLine(String.Format("{0}:{1}", "Make", res.Make));
            Console.WriteLine(string.Format("{0}:{1}", "Model", res.Model));
            Console.WriteLine(string.Format("{0}:{1}", "LensModel", res.LensModel));
            Console.WriteLine(string.Format("{0}:{1}", "FocalLength", res.FocalLength));
            Console.WriteLine(string.Format("{0}:{1}", "ShutterSpeedValue", res.ShutterSpeedValue));
            Console.WriteLine(string.Format("{0}:{1}", "ApertureValue", res.ApertureValue));
            Console.WriteLine(string.Format("{0}:{1}", "DateTime", res.DateTime));
            Console.WriteLine(string.Format("{0}:{1}", "ISO", res.ISO));
            Console.WriteLine("-------------------------------------------------------------------");
        }
        #endregion
    }
}
