using System.Text.RegularExpressions;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class PromptParserService
{
    public (ProjectConfig config, List<string> modules) Parse(string prompt)
    {
        var config = new ProjectConfig { ProjectName = "AI Query", UserCount = 100, DeploymentType = DeploymentType.Kubernetes };
        var modules = new List<string>();

        var userMatch = Regex.Match(prompt, @"(\d+)\s*(користувач|users|юзер|user)", RegexOptions.IgnoreCase);
        if (userMatch.Success)
            config.UserCount = int.Parse(userMatch.Groups[1].Value);

        if (Regex.IsMatch(prompt, @"windows", RegexOptions.IgnoreCase))
            config.DeploymentType = DeploymentType.Windows;
        else if (Regex.IsMatch(prompt, @"гібрид|hybrid|змішан", RegexOptions.IgnoreCase))
            config.DeploymentType = DeploymentType.Hybrid;

        if (Regex.IsMatch(prompt, @"performance|продуктивн|perf", RegexOptions.IgnoreCase))
            config.LoadProfile = LoadProfile.Performance;

        if (Regex.IsMatch(prompt, @"ha|high avail|відмовостій|відмов", RegexOptions.IgnoreCase))
            config.HaEnabled = true;

        if (Regex.IsMatch(prompt, @"app.?server|appserver|as\b", RegexOptions.IgnoreCase))
            modules.Add("App Server");
        if (Regex.IsMatch(prompt, @"robot|робот", RegexOptions.IgnoreCase))
            modules.Add("ROBOT");
        if (Regex.IsMatch(prompt, @"web|веб", RegexOptions.IgnoreCase))
            modules.Add("Web");
        if (Regex.IsMatch(prompt, @"bpm|force.?bpm|forcebpm", RegexOptions.IgnoreCase))
            modules.Add("ForceBPM");
        if (Regex.IsMatch(prompt, @"lms|learning|навчан", RegexOptions.IgnoreCase))
            modules.Add("LMS");
        if (Regex.IsMatch(prompt, @"hr|portal|портал|hr.?portal", RegexOptions.IgnoreCase))
            modules.Add("HR Portal");
        if (Regex.IsMatch(prompt, @"windows.?infra|windows.?server", RegexOptions.IgnoreCase))
            modules.Add("Windows Infrastructure");

        if (modules.Count == 0)
            modules.AddRange(new[] { "App Server", "Web", "ForceBPM" });

        config.SelectedModules = modules;
        return (config, modules);
    }
}