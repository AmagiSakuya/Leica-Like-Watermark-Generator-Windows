using ExifLibrary;
using leica_watermark_generator.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace leica_watermark_generator.Scripts.Core
{
    internal class UserInputManager
    {
        /// <summary>
        /// 确保Exif输入和自定义
        /// </summary>
        public static string UserSureExif(ExifTag tag)
        {
            var configData = FileManager.configData;
            var preset = GetCurrentPreset();
            if (configData.NoExifImageConfig.UsePresetInsteadRealtimeUserSelection)
            {
                if (tag == ExifTag.Make)
                {
                    return preset.Make;
                }
                else if (tag == ExifTag.Model)
                {
                    return preset.Model;
                }
                else if (tag == ExifTag.LensModel)
                {
                    return preset.LensModel;
                }
                else if (tag == ExifTag.FocalLength)
                {
                    return preset.FocalLength.ToString();
                }
                else if (tag == ExifTag.ISOSpeedRatings)
                {
                    return preset.ISOSpeed;
                }
                else if (tag == ExifTag.DateTime)
                {
                    return preset.DateTime;
                }
                else if (tag == ExifTag.ShutterSpeedValue)
                {
                    return preset.ShutterSpeedValue;
                }
                else if (tag == ExifTag.ApertureValue)
                {
                    return preset.ApertureValue;
                }
            }
            else
            {
                if (tag == ExifTag.Make)
                {
                    List<string> optionsList = new List<string>();
                    for (int i = 0; i < configData.MakerConfig.Length; i++)
                    {
                        optionsList.Add(configData.MakerConfig[i].MakerImage);
                    }
                    return GetUserSelection(optionsList.ToArray(), "Make");
                }
                else if (tag == ExifTag.Model)
                {
                    return GetUserSelection(configData.NoExifImageConfig.ModelOptions, "Model");
                }
                else if (tag == ExifTag.LensModel)
                {
                    return GetUserSelection(configData.NoExifImageConfig.LensModelOptions, "LensModel");
                }
                else if (tag == ExifTag.FocalLength)
                {
                    return GetUserSelection(configData.NoExifImageConfig.FocalLengthOptions, "FocalLength", true);
                }
                else if (tag == ExifTag.ISOSpeedRatings)
                {
                    return GetUserSelection(configData.NoExifImageConfig.ISOSpeedRatings, "ISOSpeedRatings", true);
                }
                else if (tag == ExifTag.DateTime)
                {
                    return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                }
                else if (tag == ExifTag.ShutterSpeedValue)
                {
                    return GetUserSelection(configData.NoExifImageConfig.ShutterSpeedValueOptions, "ShutterSpeedValue"); 
                }
                else if (tag == ExifTag.ApertureValue)
                {
                    return GetUserSelection(configData.NoExifImageConfig.ApertureValueOptions, "ApertureValue"); 
                }
            }
            
            return string.Empty;
        }

        public static string GetUserSelection(string[] options, string addTitle = "selection", bool includeCustomInput = false)
        {
            while (true)
            {
                Console.WriteLine("####");
                // 打印选项
                Console.WriteLine(string.Format("[{0}] Please select an option:", addTitle));
                for (int i = 0; i < options.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {options[i]}");
                }

                // 如果需要包括自定义输入选项，则显示
                if (includeCustomInput)
                {
                    Console.WriteLine($"{options.Length + 1}. Custom Input");
                }

                // 获取用户输入
                string userInput = Console.ReadLine();

                // 尝试解析用户输入为整数
                if (int.TryParse(userInput, out int selectedOption))
                {
                    // 检查输入是否在有效范围内
                    int maxOption = includeCustomInput ? options.Length + 1 : options.Length;
                    if (selectedOption >= 1 && selectedOption <= maxOption)
                    {
                        // 如果选择的是最后一个选项（自定义输入）
                        if (includeCustomInput && selectedOption == options.Length + 1)
                        {
                            Console.WriteLine("Please enter your custom input:");
                            string customInput = Console.ReadLine();
                            return customInput;
                        }
                        else
                        {
                            // 返回选中的预定义选项
                            return options[selectedOption - 1];
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Please try again.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter a number.");
                }
            }
        }

        static PresetSturct GetCurrentPreset()
        {
            var currentName = FileManager.configData.NoExifImageConfig.PresetSettings.Current;
            for (int i = 0; i < FileManager.configData.NoExifImageConfig.PresetSettings.Presets.Length; i++)
            {
                if (FileManager.configData.NoExifImageConfig.PresetSettings.Presets[i].PresetName == currentName)
                {
                    return FileManager.configData.NoExifImageConfig.PresetSettings.Presets[i];
                }
            }
            return null;
        }

    }
}