using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class CostEstimate
{
    public string Provider { get; set; } = "Azure";
    public double MonthlyCompute { get; set; }
    public double MonthlyStorage { get; set; }
    public double MonthlyTotal => MonthlyCompute + MonthlyStorage;
    public double YearlyTotal => MonthlyTotal * 12;
    public int NodeCount { get; set; }
}

public class CostEstimator
{
    private static readonly Dictionary<string, (double cpu, double ram, double hourly)> AzurePricing = new()
    {
        ["Standard_B1s"] = (1, 1, 0.0076),
        ["Standard_B2s"] = (2, 4, 0.0304),
        ["Standard_D2s_v5"] = (2, 8, 0.077),
        ["Standard_D4s_v5"] = (4, 16, 0.154),
        ["Standard_D8s_v5"] = (8, 32, 0.308),
        ["Standard_D16s_v5"] = (16, 64, 0.616),
        ["Standard_D32s_v5"] = (32, 128, 1.232),
        ["Standard_F16s_v2"] = (16, 32, 0.792),
    };

    private static readonly Dictionary<string, (double cpu, double ram, double hourly)> AwsPricing = new()
    {
        ["t3.nano"] = (2, 0.5, 0.0052),
        ["t3.micro"] = (2, 1, 0.0104),
        ["t3.small"] = (2, 2, 0.0208),
        ["t3.medium"] = (2, 4, 0.0416),
        ["t3.large"] = (2, 8, 0.0832),
        ["m6i.large"] = (2, 8, 0.080),
        ["m6i.xlarge"] = (4, 16, 0.160),
        ["m6i.2xlarge"] = (8, 32, 0.320),
    };

    private static readonly Dictionary<string, (double cpu, double ram, double hourly)> GcpPricing = new()
    {
        ["e2-micro"] = (2, 1, 0.0076),
        ["e2-small"] = (2, 4, 0.0151),
        ["e2-medium"] = (2, 4, 0.0302),
        ["e2-standard-8"] = (8, 32, 0.2416),
        ["e2-standard-16"] = (16, 64, 0.4832),
        ["e2-standard-32"] = (32, 128, 0.9664),
    };

    private static readonly double StorageGbPerMonth = 0.08;

    public CostEstimate EstimateAzure(ResourceRequirement req, ProjectConfig config)
    {
        var totalMonthly = 0.0;
        var totalStorageMonthly = 0.0;
        var nodeCount = 0;

        foreach (var infra in req.Infrastructure)
        {
            var size = ConfigExportService.GetAzureVmSize(infra.Cpu, infra.RamGb);
            if (AzurePricing.TryGetValue(size, out var price))
            {
                totalMonthly += price.hourly * 730 * infra.NodeCount;
                totalStorageMonthly += infra.TotalStorageGb * StorageGbPerMonth;
                nodeCount += infra.NodeCount;
            }
        }

        return new CostEstimate
        {
            Provider = "Azure", MonthlyCompute = Math.Round(totalMonthly, 2),
            MonthlyStorage = Math.Round(totalStorageMonthly, 2), NodeCount = nodeCount
        };
    }

    public CostEstimate EstimateAws(ResourceRequirement req, ProjectConfig config)
    {
        var totalMonthly = 0.0;
        var totalStorageMonthly = 0.0;
        var nodeCount = 0;

        foreach (var infra in req.Infrastructure)
        {
            var size = ConfigExportService.GetAwsInstanceType(infra.Cpu, infra.RamGb);
            if (AwsPricing.TryGetValue(size, out var price))
            {
                totalMonthly += price.hourly * 730 * infra.NodeCount;
                totalStorageMonthly += infra.TotalStorageGb * StorageGbPerMonth;
                nodeCount += infra.NodeCount;
            }
        }

        return new CostEstimate
        {
            Provider = "AWS", MonthlyCompute = Math.Round(totalMonthly, 2),
            MonthlyStorage = Math.Round(totalStorageMonthly, 2), NodeCount = nodeCount
        };
    }

    public CostEstimate EstimateGcp(ResourceRequirement req, ProjectConfig config)
    {
        var totalMonthly = 0.0;
        var totalStorageMonthly = 0.0;
        var nodeCount = 0;

        foreach (var infra in req.Infrastructure)
        {
            var size = ConfigExportService.GetGcpMachineType(infra.Cpu, infra.RamGb);
            if (GcpPricing.TryGetValue(size, out var price))
            {
                totalMonthly += price.hourly * 730 * infra.NodeCount;
                totalStorageMonthly += infra.TotalStorageGb * StorageGbPerMonth;
                nodeCount += infra.NodeCount;
            }
        }

        return new CostEstimate
        {
            Provider = "GCP", MonthlyCompute = Math.Round(totalMonthly, 2),
            MonthlyStorage = Math.Round(totalStorageMonthly, 2), NodeCount = nodeCount
        };
    }
}
