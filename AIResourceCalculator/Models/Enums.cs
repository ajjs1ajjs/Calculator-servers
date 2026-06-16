namespace AIResourceCalculator.Models;

public enum DeploymentType
{
    Kubernetes,
    Windows,
    Hybrid
}

public enum ProductType
{
    Standard,
    DocumentFlow
}

public enum LoadProfile
{
    Basic,
    Performance
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
