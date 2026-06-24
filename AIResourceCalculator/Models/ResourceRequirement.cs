namespace AIResourceCalculator.Models;

public class ResourceRequirement
{
    public int UserCount { get; set; }
    public DeploymentType DeploymentType { get; set; }
    public LoadProfile LoadProfile { get; set; }

    // Підсумкові ФІЗИЧНІ ресурси всіх вузлів (скільки заліза провіжинити).
    public double TotalCpu { get; set; }
    public double TotalRamGb { get; set; }
    public int TotalStorageGb { get; set; }

    // Сукупний ЗАПИТ подів K8s (requests). Для Windows = 0 (подів немає).
    // Менший за TotalCpu/TotalRamGb — вузли провіжиняться з округленням угору + master/SQL.
    public double PodCpu { get; set; }
    public double PodRamGb { get; set; }
    public int TotalIops { get; set; }
    public double TotalLatency { get; set; }
    public int WorkerNodeCount { get; set; }
    public int MasterNodeCount { get; set; }

    public List<ServiceComponent> Components { get; set; } = new();
    public List<InfrastructureNode> Infrastructure { get; set; } = new();

    // Глибока копія: інфраструктура клонується повузлово (щоб модифікації середовищ — напр.
    // бекап-резерв — не зачіпали оригінал PROD). Компоненти копіюються у новий список.
    public ResourceRequirement DeepClone()
    {
        var r = (ResourceRequirement)MemberwiseClone();
        r.Infrastructure = Infrastructure.Select(n => n.Clone()).ToList();
        r.Components = Components.ToList();
        return r;
    }


    public string Summary()
    {
        return $"[{DeploymentType} / {LoadProfile}] Users: {UserCount}\n" +
               $"CPU: {TotalCpu:F1} | RAM: {TotalRamGb:F1} GB | Storage: {TotalStorageGb} GB | IOPS: {TotalIops}\n" +
               $"Nodes: {MasterNodeCount} Master + {WorkerNodeCount} Worker";
    }
}
