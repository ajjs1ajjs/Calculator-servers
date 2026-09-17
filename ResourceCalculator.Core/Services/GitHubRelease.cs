using System.Diagnostics;
using System.IO;

namespace ResourceCalculator.Services;

// Спільні для UpdateCheckService і SelfUpdateService константи та лог.
// Раніше і URL релізів, і назва ассета, і код запису в update-check.log були
// продубльовані в обох сервісах — розсинхрон будь-якого з трьох ламав оновлення
// тихо, вже у продакшені.
internal static class GitHubRelease
{
    public const string LatestReleaseUrl =
        "https://api.github.com/repos/ajjs1ajjs/Calculator-servers/releases/latest";

    public const string ExeAssetName = "ITE.ResourceCalculator.exe";

    public static void Log(string scope, string message)
    {
        Debug.WriteLine($"{scope}: {message}");
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ResourceCalculator");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "update-check.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {scope}: {message}" + Environment.NewLine);
        }
        catch { /* logging must never break the update */ }
    }
}
