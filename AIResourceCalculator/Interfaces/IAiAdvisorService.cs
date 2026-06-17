using AIResourceCalculator.Models;

namespace AIResourceCalculator.Interfaces;

public interface IAiAdvisorService
{
    List<AiRecommendation> Analyze(ResourceRequirement req, ProjectConfig config);
    void UpdateSettings(AiSettings settings);
    Task<AiDualProfileResult> AnalyzeAsync(ResourceRequirement req, ProjectConfig config, ResourceRequirement? perfReq = null);
}
