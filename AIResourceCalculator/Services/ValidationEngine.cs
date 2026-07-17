using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class ValidationEngine : IValidationEngine
{
    // Severity thresholds expressed as ratio of allocated / required.
    private const double CriticalThreshold = 0.8;       // allocated below this fraction → CRITICAL
    private const double OverprovisionThreshold = 1.5;   // allocated above this multiple → OVERPROVISIONED

    private readonly ILocalizationService _loc;

    public ValidationEngine() : this(LocalizationService.Instance) { }

    public ValidationEngine(ILocalizationService loc) => _loc = loc;

    private void AddResult(List<ValidationResult> results, string resKey,
        double req, double alloc, string unit, Func<double, double, string> getRec)
    {
        results.Add(new ValidationResult
        {
            ResourceName = _loc[resKey],
            Required = req, Allocated = alloc, Unit = unit,
            Severity = GetSeverity(req, alloc),
            Recommendation = getRec(req, alloc)
        });
    }

    private string LF(string key, params object[] args) => string.Format(_loc[key], args);

    public List<ValidationResult> CompareProfiles(ResourceRequirement profile1, ResourceRequirement profile2)
        => Validate(profile1, profile2);

    public List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated)
    {
        var results = new List<ValidationResult>();

        AddResult(results, "val.res.vcpu", required.TotalCpu, allocated.TotalCpu, "cores", GetCpuRecommendation);
        AddResult(results, "val.res.ram", required.TotalRamGb, allocated.TotalRamGb, "GB", GetRamRecommendation);
        AddResult(results, "val.res.storage", required.TotalStorageGb, allocated.TotalStorageGb, "GB", GetStorageRecommendation);
        AddResult(results, "val.res.iops", required.TotalIops, allocated.TotalIops, "IOPS", GetRecommendation);
        AddResult(results, "val.res.workerNodes", required.WorkerNodeCount, allocated.WorkerNodeCount, "nodes", GetNodeRecommendation);
        AddResult(results, "val.res.masterNodes", required.MasterNodeCount, allocated.MasterNodeCount, "nodes", GetNodeRecommendation);

        return results;
    }

    public List<ValidationResult> ValidateProject(ProjectConfig config, ResourceRequirement calculated, List<InfrastructureNode> actualResources)
    {
        var results = new List<ValidationResult>();

        foreach (var infra in calculated.Infrastructure)
        {
            var actual = actualResources.FirstOrDefault(a =>
                a.Name.Equals(infra.Name, StringComparison.OrdinalIgnoreCase));

            if (actual == null)
            {
                results.Add(new ValidationResult
                {
                    ResourceName = $"{infra.Name} - exists",
                    Required = 1, Allocated = 0, Unit = "count",
                    Severity = "CRITICAL",
                    Recommendation = $"Missing {infra.Name} infrastructure"
                });
                continue;
            }

            results.Add(new ValidationResult
            {
                ResourceName = $"{infra.Name} - CPU",
                Required = infra.Cpu, Allocated = actual.Cpu, Unit = "cores",
                Severity = GetSeverity(infra.Cpu, actual.Cpu),
                Recommendation = GetCpuRecommendation(infra.Cpu, actual.Cpu)
            });

            results.Add(new ValidationResult
            {
                ResourceName = $"{infra.Name} - RAM",
                Required = infra.RamGb, Allocated = actual.RamGb, Unit = "GB",
                Severity = GetSeverity(infra.RamGb, actual.RamGb),
                Recommendation = GetRamRecommendation(infra.RamGb, actual.RamGb)
            });

            results.Add(new ValidationResult
            {
                ResourceName = $"{infra.Name} - count",
                Required = infra.NodeCount, Allocated = actual.NodeCount, Unit = "nodes",
                Severity = GetSeverity(infra.NodeCount, actual.NodeCount),
                Recommendation = GetNodeRecommendation(infra.NodeCount, actual.NodeCount)
            });
        }

        return results;
    }

    private string GetSeverity(double required, double allocated)
    {
        if (allocated < required * CriticalThreshold) return "CRITICAL";
        if (allocated < required) return "WARNING";
        if (allocated > required * OverprovisionThreshold) return "OVERPROVISIONED";
        return "OK";
    }

    private string GetCpuRecommendation(double req, double alloc)
    {
        if (alloc < req) return LF("val.cpu.increase", alloc, req);
        if (alloc > req * OverprovisionThreshold) return LF("val.cpu.reduce", alloc, req);
        return _loc["val.cpu.ok"];
    }

    private string GetRamRecommendation(double req, double alloc)
    {
        if (alloc < req) return LF("val.ram.increase", alloc, req);
        if (alloc > req * OverprovisionThreshold) return LF("val.ram.reduce", alloc, req);
        return _loc["val.ram.ok"];
    }

    private string GetStorageRecommendation(double req, double alloc)
    {
        if (alloc < req) return LF("val.storage.increase", alloc, req);
        if (alloc > req * 2) return LF("val.storage.reduce", alloc, req);
        return _loc["val.storage.ok"];
    }

    private string GetNodeRecommendation(double req, double alloc)
    {
        if (alloc < req) return LF("val.nodes.increase", alloc, req);
        if (alloc > req * 2) return LF("val.nodes.reduce", alloc, req);
        return _loc["val.nodes.ok"];
    }

    private string GetRecommendation(double req, double alloc)
    {
        if (alloc < req) return LF("val.generic.increase", alloc, req);
        return _loc["val.generic.ok"];
    }
}
