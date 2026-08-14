namespace ResourceCalculator.Models;

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

    // Обсяг холодних/архівних даних Content (ГБ), заданий вручну. 0 = не задано (фіксоване
    // значення з матриці). Актуально лише для PROD — у non-prod диск Content не виділяється.
    public int ContentDbSizeGb { get; set; }

    // Чи включати розділи/аркуші з компонентами (подами) у сформований звіт (Excel/PDF).
    // На сам розрахунок не впливає — лише на те, що потрапляє у файл звіту.
    public bool IncludeComponentsInReport { get; set; } = true;
}
