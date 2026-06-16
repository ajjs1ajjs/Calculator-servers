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

        var hasK8s = Regex.IsMatch(prompt, @"\b(k8s|kubernetes|кубер)\b", RegexOptions.IgnoreCase);
        var hasWindows = Regex.IsMatch(prompt, @"\bwindows\b(?!\s+infra|infrastructure)", RegexOptions.IgnoreCase);
        var hasHybrid = Regex.IsMatch(prompt, @"\b(гібрид|hybrid|змішан)\b", RegexOptions.IgnoreCase);

        if (hasHybrid)
            config.DeploymentType = DeploymentType.Hybrid;
        else if (hasWindows && !hasK8s)
            config.DeploymentType = DeploymentType.Windows;

        var hasPerformance = Regex.IsMatch(prompt, @"\b(performance|продуктивн|perf|документообіг|document\s*flow)\b", RegexOptions.IgnoreCase);
        if (hasPerformance)
        {
            config.LoadProfile = LoadProfile.Performance;
            config.ProductType = ProductType.DocumentFlow;
        }
        else
        {
            config.ProductType = ProductType.Standard;
        }

        if (Regex.IsMatch(prompt, @"\b(app\s*server|appserver)\b", RegexOptions.IgnoreCase))
            modules.Add("App Server");
        if (Regex.IsMatch(prompt, @"\b(robot|робот)\b", RegexOptions.IgnoreCase))
            modules.Add("ROBOT");
        if (Regex.IsMatch(prompt, @"\bweb\b|веб", RegexOptions.IgnoreCase))
            modules.Add("Web");
        if (Regex.IsMatch(prompt, @"\b(bpm|force\s*bpm|forcebpm)\b", RegexOptions.IgnoreCase))
            modules.Add("ForceBPM");
        if (Regex.IsMatch(prompt, @"\b(lms|learning|навчан)\b", RegexOptions.IgnoreCase))
            modules.Add("LMS");
        if (Regex.IsMatch(prompt, @"\bhr\b|портал|hr\s*portal", RegexOptions.IgnoreCase))
            modules.Add("HR Portal");
        if (Regex.IsMatch(prompt, @"\bwindows\s+(infra|infrastructure|server)\b", RegexOptions.IgnoreCase))
            modules.Add("Windows Infrastructure");

        if (modules.Count == 0)
            modules.AddRange(new[] { "App Server", "Web", "ForceBPM" });

        config.SelectedModules = modules;
        return (config, modules);
    }
}