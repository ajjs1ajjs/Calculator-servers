using ResourceCalculator.Models;

namespace ResourceCalculator.Interfaces;

public interface ICalculationHistoryService
{
    List<CalculationHistoryItem> LoadHistory();
    void SaveToHistory(ProjectConfig config, ResourceRequirement req);
}
