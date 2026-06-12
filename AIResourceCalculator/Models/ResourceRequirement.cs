namespace AIResourceCalculator.Models;

public class ResourceRequirement
{
    public int UserCount { get; set; }
    public DeploymentType DeploymentType { get; set; }
    public LoadProfile LoadProfile { get; set; }

    public double TotalCpu { get; set; }
    public double TotalRamGb { get; set; }
    public int TotalStorageGb { get; set; }
    public int TotalIops { get; set; }
    public double TotalLatency { get; set; }
    public int WorkerNodeCount { get; set; }
    public int MasterNodeCount { get; set; }

    public List<ServiceComponent> Components { get; set; } = new();
    public List<InfrastructureNode> Infrastructure { get; set; } = new();


    public string Summary()
    {
        return $"[{DeploymentType} / {LoadProfile}] Users: {UserCount}\n" +
               $"vCPU: {TotalCpu:F1} | RAM: {TotalRamGb:F1} GB | Storage: {TotalStorageGb} GB | IOPS: {TotalIops}\n" +
               $"Nodes: {MasterNodeCount} Master + {WorkerNodeCount} Worker";
    }
}
