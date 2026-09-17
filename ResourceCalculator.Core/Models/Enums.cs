namespace ResourceCalculator.Models;

public enum DeploymentType
{
    Kubernetes,
    Windows,
    Hybrid
}

public enum LoadProfile
{
    Performance
}

public enum DatabaseType
{
    MsSql,
    PostgreSQL,
    Oracle
}

public enum DeployEnvironment
{
    Prod,
    Dev,
    Test,
    PredProd
}

// Роль вузла в матриці — явна, а не вгадана за назвою.
//
// Раніше MatrixManager.ApplyNodes роздавав відредаговані гріди по слотах матриці
// через Name.Contains("SQL"/"Сервер"/"Web"...). Назви редагує користувач, і вони
// перетинаються: "Веб сервери (IIS)" містить "сервер", тому падав у гілку
// сервера додатків — правки веб-вузла губилися, а сервер додатків отримував
// чужі диски й назву. Slot прив'язує рядок гріда до слота однозначно.
public enum NodeSlot
{
    None = 0,
    K8sSql,
    K8sMaster,
    K8sWorker,
    WindowsSql,
    WindowsApp,
    WindowsWeb,
    ReportingServer,
    HaProxy
}
