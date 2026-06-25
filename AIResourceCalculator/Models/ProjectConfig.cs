namespace AIResourceCalculator.Models;

public class ProjectConfig
{
    public string ProjectName { get; set; } = "";
    public int UserCount { get; set; }
    public DeploymentType DeploymentType { get; set; } = DeploymentType.Kubernetes;
    public ProductType ProductType { get; set; } = ProductType.Standard;
    public LoadProfile LoadProfile { get; set; } = LoadProfile.Basic;
    public DatabaseType DatabaseType { get; set; } = DatabaseType.MsSql;
    public bool HaEnabled { get; set; } = true;
    public List<string> SelectedModules { get; set; } = new();

    // Опціональні інфраструктурні вузли — типово ВИМКНЕНІ, вмикаються перемикачем (як модулі
    // LMS/HR Portal). Додаються лише коли увімкнені; на базовий розрахунок інакше не впливають.
    public bool IncludeReportingServer { get; set; }   // Сервер звітів (Reporting Services), 2/4
    public bool IncludeSqlFailover { get; set; }       // Другий вузол БД (failover-кластер)
    public bool IncludeHaProxy { get; set; }           // Балансувальник HAProxy (Linux), 2/4

    // Середовище, для якого виконується розрахунок. Визначає редакцію СУБД:
    // non-prod (DEV/TEST/PreProd) → Developer Edition; PROD → Standard/Enterprise.
    public DeployEnvironment Environment { get; set; } = DeployEnvironment.Prod;
}
