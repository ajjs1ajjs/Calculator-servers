namespace AIResourceCalculator.Models;

public class AiDualProfileResult
{
    public AiProfileResult Balance { get; set; } = new();
    public AiProfileResult Performance { get; set; } = new();
}

public class AiProfileResult
{
    public List<AiRecommendation> Recommendations { get; set; } = new();
    public List<InfrastructureNode> Infrastructure { get; set; } = new();
}
