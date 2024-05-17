using leica_watermark_generator.Scripts.Core;
using leica_watermark_generator.Scripts.DataModel;

Console.WriteLine("-------使用说明-------");
Console.WriteLine("01.将图片存放在 [程序目录/UserData/Input/(对应机型)]");
Console.WriteLine("02.运行程序，读取到图片，按任意键继续开始处理");
Console.WriteLine("[对应机型]等更多高级参数 可以在[程序目录]-[config.json] 中配置");
Console.WriteLine("-----------------");

string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

List<string> sourceDirs = new List<string>();
List<string> outputDirs = new List<string>();
List<string[]> filesList = new List<string[]>();

for (int i = 0; i < FileManager.configData.MakerConfig.Length; i++)
{
    string relative_input_path = String.Format("UserData\\Input\\{0}", FileManager.configData.MakerConfig[i].MakerName);
    string sourceDir = Path.Combine(currentDirectory, relative_input_path);
    FileManager.EnSureDir(sourceDir, relative_input_path);
    sourceDirs.Add(sourceDir);

    string[] files = FileManager.ReadDirJPGs(sourceDir, FileManager.configData.MakerConfig[i].MakerName);
    filesList.Add(files);

    string relative_output_path = String.Format("UserData\\Output\\{0}", FileManager.configData.MakerConfig[i].MakerName);
    string outputDir = Path.Combine(currentDirectory, relative_output_path);
    FileManager.EnSureDir(outputDir, relative_output_path);
    outputDirs.Add(outputDir);

}

Console.WriteLine("-----------------");
Console.WriteLine("Press any key to continue...");
// 等待用户按下任意键
Console.ReadKey();

for (int i = 0; i < filesList.Count; i++)
{
    foreach (string file in filesList[i])
    {
        var filename = Path.GetFileNameWithoutExtension(file);
        Console.WriteLine("-----------------");
        Console.WriteLine($"Loading [{filename}.jpg] EXIF info");
        ExifInfo exif_info = FileManager.LoadExif(file);
        exif_info.Make = FileManager.configData.MakerConfig[i].MakerName;
        exif_info.MakeImagePath = FileManager.configData.MakerConfig[i].MakerImage;
        exif_info.FileName = filename;
        FileManager.LogImageInfo(exif_info);
        Draw.DrawImg(exif_info, file, outputDirs[i]);
    }
}

Console.WriteLine("-----------------");
Console.WriteLine("Done! Press any key to close");
Console.ReadKey();