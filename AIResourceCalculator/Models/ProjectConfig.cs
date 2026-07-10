namespace AIResourceCalculator.Models;

public class ProjectConfig
{
    public string ProjectName { get; set; } = "";
    public int UserCount { get; set; }
    public DeploymentType DeploymentType { get; set; } = DeploymentType.Kubernetes;
    public LoadProfile LoadProfile { get; set; } = LoadProfile.Performance;
    public DatabaseType DatabaseType { get; set; } = DatabaseType.MsSql;
    public List<string> SelectedModules { get; set; } = new();

    // Опціональні інфраструктурні вузли — типово ВИМКНЕНІ, вмикаються перемикачем (як модулі
    // LMS/HR Portal). Додаються лише коли увімкнені; на базовий розрахунок інакше не впливають.
    public bool IncludeReportingServer { get; set; }   // Сервер звітів (Reporting Services), 2/4
    public bool IncludeSqlFailover { get; set; }       // Другий вузол БД (failover-кластер)
    public bool IncludeHaProxy { get; set; }           // Балансувальник HAProxy (Linux), 2/4 — завжди 1 вузол

    // Середовище, для якого виконується розрахунок. Визначає редакцію СУБД:
    // non-prod (DEV/TEST/PreProd) → Developer Edition; PROD → Standard/Enterprise.
    public DeployEnvironment Environment { get; set; } = DeployEnvironment.Prod;

    // Обсяг даних БД (ГБ), заданий вручну для цього середовища. 0 = не задано (диски беруться
    // фіксованими з матриці, як і раніше). Test/PreProd за замовчуванням = Prod, не менше Prod.
    public int DbSizeGb { get; set; }
}
