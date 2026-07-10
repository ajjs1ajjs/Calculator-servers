using AIResourceCalculator.Data;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Interfaces;

public interface ISizingEngine
{
    IReadOnlyList<ProjectModule> Modules { get; }
    void SetModules(List<ProjectModule> modules);
    void ReloadModules();
    ResourceRequirement Calculate(ProjectConfig config);
}
