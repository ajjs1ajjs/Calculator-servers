namespace ResourceCalculator.Models;

public static class ModuleExtensions
{
    public static List<ProjectModule> ToClonedList(this IEnumerable<ProjectModule> modules)
        => modules.Select(m => m.Clone()).ToList();
}