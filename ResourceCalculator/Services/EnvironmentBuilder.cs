using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

/// <summary>
/// Будує звіти середовищ (PROD/DEV/TEST/PreProd) для MainViewModel.
/// КОЖНЕ середовище рахується рушієм ОКРЕМО за власною кількістю користувачів —
/// як у Excel-табличці, а не масштабуванням PROD.
/// </summary>
public class EnvironmentBuilder
{
    private readonly ISizingEngine _engine;
    private readonly ILocalizationService _loc;

    public EnvironmentBuilder(ISizingEngine engine, ILocalizationService loc)
    {
        _engine = engine;
        _loc = loc;
    }

    /// <summary>
    /// Парсить рядкові поля введення користувача в EnvironmentSettings.
    /// </summary>
    public EnvironmentSettings ParseSettings(
        string devUserCount, string testUserCount, string predProdUserCount,
        string prodDbSizeGb, string devDbSizeGb, string testDbSizeGb, string predProdDbSizeGb,
        string devContentDbSizeGb, string testContentDbSizeGb, string predProdContentDbSizeGb,
        bool includeDev, bool includeTest, bool includePredProd,
        out int resolvedTestDbSize, out int resolvedPredProdDbSize, out int resolvedDevDbSize)
    {
        if (!int.TryParse(devUserCount, out var dev) || dev < 1) dev = 10;
        if (!int.TryParse(testUserCount, out var test) || test < 1) test = 25;
        if (!int.TryParse(predProdUserCount, out var pp) || pp < 1) pp = 50;

        if (!int.TryParse(prodDbSizeGb, out var prodDbSize) || prodDbSize < 0) prodDbSize = 0;
        if (!int.TryParse(devDbSizeGb, out var devDb) || devDb < 0) devDb = prodDbSize;
        if (!int.TryParse(testDbSizeGb, out var testDb) || testDb <= 0) testDb = prodDbSize;
        if (!int.TryParse(predProdDbSizeGb, out var ppDb) || ppDb <= 0) ppDb = prodDbSize;
        testDb = Math.Max(testDb, prodDbSize);
        ppDb = Math.Max(ppDb, prodDbSize);

        if (!int.TryParse(devContentDbSizeGb, out var devContent) || devContent < 0) devContent = 0;
        if (!int.TryParse(testContentDbSizeGb, out var testContent) || testContent < 0) testContent = 0;
        if (!int.TryParse(predProdContentDbSizeGb, out var ppContent) || ppContent < 0) ppContent = 0;

        resolvedTestDbSize = testDb;
        resolvedPredProdDbSize = ppDb;
        resolvedDevDbSize = devDb;

        return new EnvironmentSettings
        {
            IncludeDev = includeDev,
            IncludeTest = includeTest,
            IncludePredProd = includePredProd,
            DevUserCount = Math.Clamp(dev, 1, 5000),
            TestUserCount = Math.Clamp(test, 1, 5000),
            PredProdUserCount = Math.Clamp(pp, 1, 5000),
            DevDbSizeGb = devDb,
            TestDbSizeGb = testDb,
            PredProdDbSizeGb = ppDb,
            DevContentDbSizeGb = devContent,
            TestContentDbSizeGb = testContent,
            PredProdContentDbSizeGb = ppContent
        };
    }

    /// <summary>
    /// Будує всі звіти середовищ: PROD (завжди) + DEV/TEST/PreProd (за вибором).
    /// Використовує _engine.Calculate() для кожного середовища, тимчасово змінюючи модулі.
    /// Після завершення відновлює модулі до PROD-стану.
    /// </summary>
    public List<EnvironmentReport> Build(
        ProjectConfig config, ResourceRequirement prodReq,
        EnvironmentSettings settings,
        IEnumerable<ProjectModule> modules,
        IEnumerable<EnvModuleCount> envModuleCounts,
        IEnumerable<EnvNodeToggle> envNodeToggles)
    {
        // PROD: к-сті опціональних модулів — з полів модулів угорі (0/порожньо = усі користувачі).
        var prodMods = string.Join(" · ", modules.Where(m => !m.IsMandatory && m.IsEnabled)
            .Select(m => $"{m.Name}: {(m.UserCount > 0 ? m.UserCount.ToString() : "усі")}"));

        var reports = new List<EnvironmentReport>
        {
            new() { Environment = DeployEnvironment.Prod, Name = "PROD",
                    UserCount = config.UserCount, Requirement = prodReq, ModulesInfo = prodMods }
        };

        if (settings.IncludePredProd)
            reports.Add(BuildEnv(DeployEnvironment.PredProd, "PreProd", settings.PredProdUserCount,
                config, settings, modules, envModuleCounts, envNodeToggles));
        if (settings.IncludeTest)
            reports.Add(BuildEnv(DeployEnvironment.Test, "TEST", settings.TestUserCount,
                config, settings, modules, envModuleCounts, envNodeToggles));
        if (settings.IncludeDev)
            reports.Add(BuildEnv(DeployEnvironment.Dev, "DEV", settings.DevUserCount,
                config, settings, modules, envModuleCounts, envNodeToggles));

        // Порядок: PROD → PreProd → TEST → DEV
        static int EnvOrder(DeployEnvironment e) => e switch
        {
            DeployEnvironment.Prod => 0,
            DeployEnvironment.PredProd => 1,
            DeployEnvironment.Test => 2,
            DeployEnvironment.Dev => 3,
            _ => 9
        };
        reports = reports.OrderBy(r => EnvOrder(r.Environment)).ToList();

        // Відновити стан рушія до PROD-конфігурації
        _engine.SetModules(modules.ToList());

        return reports;
    }

    private EnvironmentReport BuildEnv(
        DeployEnvironment env, string name, int users,
        ProjectConfig config, EnvironmentSettings settings,
        IEnumerable<ProjectModule> modules,
        IEnumerable<EnvModuleCount> envModuleCounts,
        IEnumerable<EnvNodeToggle> envNodeToggles)
    {
        var envDbSize = env switch
        {
            DeployEnvironment.Dev => settings.DevDbSizeGb,
            DeployEnvironment.Test => settings.TestDbSizeGb,
            DeployEnvironment.PredProd => settings.PredProdDbSizeGb,
            _ => config.DbSizeGb
        };
        var envContentSize = env switch
        {
            DeployEnvironment.Dev => settings.DevContentDbSizeGb,
            DeployEnvironment.Test => settings.TestContentDbSizeGb,
            DeployEnvironment.PredProd => settings.PredProdContentDbSizeGb,
            _ => config.ContentDbSizeGb
        };

        var envConfig = new ProjectConfig
        {
            ProjectName = config.ProjectName, UserCount = users,
            DeploymentType = config.DeploymentType,
            LoadProfile = config.LoadProfile, DatabaseType = config.DatabaseType,
            Environment = env,
            DbSizeGb = envDbSize,
            ContentDbSizeGb = envContentSize,
            IncludeReportingServer = NodeEnabledFor(envNodeToggles, "reporting", env),
            IncludeSqlFailover = NodeEnabledFor(envNodeToggles, "failover", env),
            IncludeHaProxy = NodeEnabledFor(envNodeToggles, "haproxy", env)
        };

        // Похідне середовище має ВЛАСНІ к-сті користувачів по модулях (LMS/HR/ForceBPM)
        var envModules = modules.Select(m => m.Clone()).ToList();
        foreach (var m in envModules)
        {
            var rowx = envModuleCounts.FirstOrDefault(r => r.ModuleName == m.Name);
            if (rowx == null) continue;
            var cnt = rowx.CountFor(env);
            if (!rowx.EnabledFor(env) || cnt <= 0) m.IsEnabled = false;
            else m.UserCount = Math.Clamp(cnt, 1, 5000);
        }
        _engine.SetModules(envModules);
        var req = _engine.Calculate(envConfig);

        var mods = string.Join(" · ", envModuleCounts
            .Where(r => r.EnabledFor(env) && r.CountFor(env) > 0)
            .Select(r => $"{r.ModuleName}: {r.CountFor(env)}"));

        return new EnvironmentReport
        {
            Environment = env, Name = name, UserCount = users,
            Requirement = req, ModulesInfo = mods
        };
    }

    private static bool NodeEnabledFor(IEnumerable<EnvNodeToggle> toggles, string key, DeployEnvironment env)
        => toggles.FirstOrDefault(r => r.Key == key)?.EnabledFor(env) ?? false;
}
