namespace AIResourceCalculator.Models;

public enum DeploymentType
{
    Kubernetes,
    Windows,
    Hybrid
}

public enum LoadProfile
{
    Basic,
    Performance
}

public enum DatabaseType
{
    MsSql,
    PostgreSQL,
    Oracle
}

public enum AiProvider
{
    None,
    OpenAI,
    Claude,
    Google,
    LocalOllama,
    DeepSeek
}

public enum DeployEnvironment
{
    Prod,
    Dev,
    Test,
    PredProd
}
