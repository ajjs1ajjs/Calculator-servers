using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class ValidationEngine
{
    public List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated)
    {
        var results = new List<ValidationResult>();

        results.Add(new ValidationResult
        {
            ResourceName = "vCPU",
            Required = required.TotalCpu,
            Allocated = allocated.TotalCpu,
            Unit = "cores",
            Severity = GetSeverity(required.TotalCpu, allocated.TotalCpu),
            Recommendation = GetCpuRecommendation(required.TotalCpu, allocated.TotalCpu)
        });

        results.Add(new ValidationResult
        {
            ResourceName = "RAM",
            Required = required.TotalRamGb,
            Allocated = allocated.TotalRamGb,
            Unit = "GB",
            Severity = GetSeverity(required.TotalRamGb, allocated.TotalRamGb),
            Recommendation = GetRamRecommendation(required.TotalRamGb, allocated.TotalRamGb)
        });

        results.Add(new ValidationResult
        {
            ResourceName = "Storage",
            Required = required.TotalStorageGb,
            Allocated = allocated.TotalStorageGb,
            Unit = "GB",
            Severity = GetSeverity(required.TotalStorageGb, allocated.TotalStorageGb),
            Recommendation = GetStorageRecommendation(required.TotalStorageGb, allocated.TotalStorageGb)
        });

        results.Add(new ValidationResult
        {
            ResourceName = "IOPS",
            Required = required.TotalIops,
            Allocated = allocated.TotalIops,
            Unit = "IOPS",
            Severity = GetSeverity(required.TotalIops, allocated.TotalIops),
            Recommendation = GetRecommendation(required.TotalIops, allocated.TotalIops)
        });

        results.Add(new ValidationResult
        {
            ResourceName = "Worker Nodes",
            Required = required.WorkerNodeCount,
            Allocated = allocated.WorkerNodeCount,
            Unit = "nodes",
            Severity = GetSeverity(required.WorkerNodeCount, allocated.WorkerNodeCount),
            Recommendation = GetNodeRecommendation(required.WorkerNodeCount, allocated.WorkerNodeCount)
        });

        results.Add(new ValidationResult
        {
            ResourceName = "Master Nodes",
            Required = required.MasterNodeCount,
            Allocated = allocated.MasterNodeCount,
            Unit = "nodes",
            Severity = GetSeverity(required.MasterNodeCount, allocated.MasterNodeCount),
            Recommendation = GetNodeRecommendation(required.MasterNodeCount, allocated.MasterNodeCount)
        });

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
                ResourceName = $"{infra.Name} - vCPU",
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
        if (allocated < required * 0.8) return "CRITICAL";
        if (allocated < required) return "WARNING";
        if (allocated > required * 1.5) return "OVERPROVISIONED";
        return "OK";
    }

    private string GetCpuRecommendation(double req, double alloc)
    {
        if (alloc < req) return $"Increase vCPU from {alloc} to at least {req} cores";
        if (alloc > req * 1.5) return $"Reduce vCPU from {alloc} to ~{req} cores to save costs";
        return "CPU resources are adequate";
    }

    private string GetRamRecommendation(double req, double alloc)
    {
        if (alloc < req) return $"Increase RAM from {alloc} to at least {req} GB";
        if (alloc > req * 1.5) return $"Reduce RAM from {alloc} to ~{req} GB to save costs";
        return "RAM resources are adequate";
    }

    private string GetStorageRecommendation(double req, double alloc)
    {
        if (alloc < req) return $"Increase storage from {alloc} to at least {req} GB";
        if (alloc > req * 2) return $"Reduce storage from {alloc} to ~{req} GB";
        return "Storage resources are adequate";
    }

    private string GetNodeRecommendation(double req, double alloc)
    {
        if (alloc < req) return $"Increase nodes from {alloc} to at least {req} for HA";
        if (alloc > req * 2) return $"Consider reducing nodes from {alloc} to ~{req}";
        return "Node count is adequate";
    }

    private string GetRecommendation(double req, double alloc)
    {
        if (alloc < req) return $"Increase from {alloc} to at least {req}";
        return "Resources are adequate";
    }
}
