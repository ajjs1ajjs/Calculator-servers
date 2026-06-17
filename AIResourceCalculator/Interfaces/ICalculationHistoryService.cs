using AIResourceCalculator.Models;

namespace AIResourceCalculator.Interfaces;

public interface ICalculationHistoryService
{
    List<CalculationHistoryItem> LoadHistory();
    void SaveToHistory(ProjectConfig config, ResourceRequirement req);
}
