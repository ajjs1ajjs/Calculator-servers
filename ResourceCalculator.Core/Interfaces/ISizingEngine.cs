using ResourceCalculator.Data;
using ResourceCalculator.Models;

namespace ResourceCalculator.Interfaces;

public interface ISizingEngine
{
    IReadOnlyList<ProjectModule> Modules { get; }
    void SetModules(List<ProjectModule> modules);
    void ReloadModules();
    ResourceRequirement Calculate(ProjectConfig config);
}
