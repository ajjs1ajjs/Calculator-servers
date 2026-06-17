using AIResourceCalculator.Data;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Interfaces;

public interface ISizingEngine
{
    IReadOnlyList<ProjectModule> Modules { get; }
    ProductType CurrentProduct { get; }
    void SetModules(List<ProjectModule> modules);
    void SetProductType(ProductType productType);
    ResourceRequirement Calculate(ProjectConfig config);
}
