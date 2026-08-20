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
